#region

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

#endregion

namespace UniConfig
{
    internal sealed class StreamingAssetsLoader : MonoBehaviour
    {
        private AsyncOperation _asyncOperation;
        private Action<Dictionary<int, object>> _onCompleted;
        private Parser _parser;
        private UnityWebRequest _request;

        public AsyncOperation RunLoader(string assetPath, int bufferSize, Action<Dictionary<int, object>> onCompleted)
        {
            _onCompleted = onCompleted;
            int size = Math.Max(256, bufferSize);
            _parser = new Parser(size);
            _request = UnityWebRequest.Get(GetStreamingAssetsURL(assetPath));
            _request.downloadHandler = _parser;
            _asyncOperation = _request.SendWebRequest();
            StartCoroutine(WaitCompleted());
            return _asyncOperation;
        }

        private IEnumerator WaitCompleted()
        {
            while (_asyncOperation != null && !_asyncOperation.isDone)
                yield return null;

            while (_parser != null && !_parser.Finished)
                yield return null;

            try
            {
                if (_request != null && _request.result != UnityWebRequest.Result.Success)
                    throw new IOException($"StreamingAssets 下载失败: {_request.error}");
                if (_parser?.Error != null)
                    throw _parser.Error;

                _onCompleted?.Invoke(_parser?.Configs ?? new Dictionary<int, object>());
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
                _onCompleted?.Invoke(new Dictionary<int, object>());
            }
            finally
            {
                _request?.Dispose();
                _asyncOperation = null;
                _request = null;
                _parser = null;
                _onCompleted = null;
                Destroy(gameObject);
            }
        }

        internal static string GetStreamingAssetsURL(string assetPath)
        {
            string full = Path.Combine(Application.streamingAssetsPath, assetPath);
#if UNITY_ANDROID && !UNITY_EDITOR
            return full;
#else
            return "file://" + full;
#endif
        }

        private sealed class Parser : DownloadHandlerScript
        {
            private readonly Dictionary<int, object> _configs = new();
            private List<object> _currentRows;

            private int _currentTableId;
            private int _payloadFilled;

            private int _payloadLength;
            private byte[] _payloadScratch;

            private byte[] _pending = Array.Empty<byte>();
            private int _pendingLength;
            private int _rowCount;
            private int _rowsParsed;
            private State _state = State.NeedMagic;

            public Parser(int bufferSize) : base(new byte[bufferSize])
            {
            }

            public Dictionary<int, object> Configs => _configs;
            public bool Finished { get; private set; }
            public Exception Error { get; private set; }

            protected override bool ReceiveData(byte[] data, int dataLength)
            {
                if (Finished || Error != null) return false;
                if (data == null || dataLength <= 0) return true;

                try
                {
                    if (_pendingLength == 0)
                    {
                        int consumed = Consume(data, 0, dataLength);
                        int remain = dataLength - consumed;
                        if (remain > 0)
                        {
                            EnsurePendingCapacity(remain);
                            Buffer.BlockCopy(data, consumed, _pending, 0, remain);
                            _pendingLength = remain;
                        }
                    }
                    else
                    {
                        EnsurePendingCapacity(_pendingLength + dataLength);
                        Buffer.BlockCopy(data, 0, _pending, _pendingLength, dataLength);
                        _pendingLength += dataLength;
                        int consumed = Consume(_pending, 0, _pendingLength);
                        int remain = _pendingLength - consumed;
                        if (remain > 0 && consumed > 0)
                            Buffer.BlockCopy(_pending, consumed, _pending, 0, remain);
                        _pendingLength = remain;
                    }
                }
                catch (Exception ex)
                {
                    Error = ex;
                    Finished = true;
                    return false;
                }

                return true;
            }

            protected override void CompleteContent()
            {
                try
                {
                    if (Error != null)
                    {
                        Finished = true;
                        return;
                    }

                    if (_pendingLength > 0)
                    {
                        int consumed = Consume(_pending, 0, _pendingLength);
                        if (consumed != _pendingLength)
                            throw new InvalidDataException("配置文件末尾存在未解析完的数据");
                        _pendingLength = 0;
                    }

                    if (_state != State.NeedMagic)
                        throw new InvalidDataException("配置文件在表数据未完成时结束");

                    Finished = true;
                }
                catch (Exception ex)
                {
                    Error = ex;
                    Finished = true;
                }
            }

            private int Consume(byte[] buffer, int start, int end)
            {
                int offset = start;
                while (offset < end)
                {
                    int before = offset;
                    if (!TryStep(buffer, ref offset, end))
                    {
                        offset = before;
                        break;
                    }
                }

                return offset - start;
            }

            private bool TryStep(byte[] buffer, ref int offset, int end)
            {
                switch (_state)
                {
                    case State.NeedMagic:
                        return TryReadMagic(buffer, ref offset, end);
                    case State.NeedTableId:
                        return TryReadTableId(buffer, ref offset, end);
                    case State.NeedCount:
                        return TryReadCount(buffer, ref offset, end);
                    case State.NeedPayloadLength:
                        return TryReadPayloadLength(buffer, ref offset, end);
                    case State.NeedPayload:
                        return TryReadPayload(buffer, ref offset, end);
                    default:
                        throw new InvalidOperationException($"未知解析状态: {_state}");
                }
            }

            private bool TryReadMagic(byte[] buffer, ref int offset, int end)
            {
                if (end - offset < 4) return false;
                byte[] magic = ConfigBinaryProtocol.Magic;
                if (buffer[offset] != magic[0] || buffer[offset + 1] != magic[1] ||
                    buffer[offset + 2] != magic[2] || buffer[offset + 3] != magic[3])
                    throw new InvalidDataException(
                        $"配置文件魔数不匹配，期望 UCFG，实际 {(char)buffer[offset]}{(char)buffer[offset + 1]}{(char)buffer[offset + 2]}{(char)buffer[offset + 3]}");

                offset += 4;
                _state = State.NeedTableId;
                return true;
            }

            private bool TryReadTableId(byte[] buffer, ref int offset, int end)
            {
                if (end - offset < 4) return false;
                _currentTableId = BitConverter.ToInt32(buffer, offset);
                offset += 4;
                if (_currentTableId <= 0)
                    throw new InvalidDataException($"表 Id 非法: {_currentTableId}");
                _state = State.NeedCount;
                return true;
            }

            private bool TryReadCount(byte[] buffer, ref int offset, int end)
            {
                if (end - offset < 4) return false;
                _rowCount = BitConverter.ToInt32(buffer, offset);
                offset += 4;
                if (_rowCount < 0)
                    throw new InvalidDataException($"表 Id={_currentTableId} 行数非法: {_rowCount}");

                _rowsParsed = 0;
                _currentRows = new List<object>(_rowCount);
                if (_rowCount == 0)
                {
                    FinishCurrentTable();
                    return true;
                }

                _state = State.NeedPayloadLength;
                return true;
            }

            private bool TryReadPayloadLength(byte[] buffer, ref int offset, int end)
            {
                if (end - offset < 4) return false;
                _payloadLength = BitConverter.ToInt32(buffer, offset);
                offset += 4;
                if (_payloadLength < 0)
                    throw new InvalidDataException($"表 Id={_currentTableId} payload 长度非法: {_payloadLength}");

                _payloadFilled = 0;
                _state = State.NeedPayload;
                return true;
            }

            private bool TryReadPayload(byte[] buffer, ref int offset, int end)
            {
                int available = end - offset;
                int need = _payloadLength - _payloadFilled;
                if (need <= 0)
                    throw new InvalidDataException("payload 内部状态错误");

                if (_payloadFilled == 0 && available >= _payloadLength)
                {
                    IConfigTable item = ConfigBinaryProtocol.DeserializeRow(
                        _currentTableId,
                        new ReadOnlySpan<byte>(buffer, offset, _payloadLength));
                    offset += _payloadLength;
                    AddRow(item);
                    return true;
                }

                if (available == 0)
                    return false;

                if (available < need)
                {
                    EnsurePayloadScratch(_payloadLength);
                    Buffer.BlockCopy(buffer, offset, _payloadScratch, _payloadFilled, available);
                    _payloadFilled += available;
                    offset += available;
                    return true;
                }

                EnsurePayloadScratch(_payloadLength);
                Buffer.BlockCopy(buffer, offset, _payloadScratch, _payloadFilled, need);
                offset += need;
                _payloadFilled = _payloadLength;
                IConfigTable assembled = ConfigBinaryProtocol.DeserializeRow(
                    _currentTableId,
                    new ReadOnlySpan<byte>(_payloadScratch, 0, _payloadLength));
                AddRow(assembled);
                return true;
            }

            private void AddRow(IConfigTable item)
            {
                if (item != null)
                    _currentRows.Add(item);
                _rowsParsed++;
                if (_rowsParsed >= _rowCount)
                    FinishCurrentTable();
                else
                    _state = State.NeedPayloadLength;
            }

            private void FinishCurrentTable()
            {
                _configs[_currentTableId] = _currentRows;
                _currentTableId = 0;
                _currentRows = null;
                _rowCount = 0;
                _rowsParsed = 0;
                _payloadLength = 0;
                _payloadFilled = 0;
                _state = State.NeedMagic;
            }

            private void EnsurePendingCapacity(int capacity)
            {
                if (_pending.Length >= capacity) return;
                int newSize = ConfigBinaryProtocol.NextPowerOfTwo(capacity);
                byte[] next = new byte[newSize];
                if (_pendingLength > 0)
                    Buffer.BlockCopy(_pending, 0, next, 0, _pendingLength);
                _pending = next;
            }

            private void EnsurePayloadScratch(int capacity)
            {
                if (_payloadScratch != null && _payloadScratch.Length >= capacity) return;
                int newSize = ConfigBinaryProtocol.NextPowerOfTwo(capacity);
                byte[] next = new byte[newSize];
                if (_payloadScratch != null && _payloadFilled > 0)
                    Buffer.BlockCopy(_payloadScratch, 0, next, 0, _payloadFilled);
                _payloadScratch = next;
            }

            private enum State
            {
                NeedMagic,
                NeedTableId,
                NeedCount,
                NeedPayloadLength,
                NeedPayload
            }
        }
    }
}