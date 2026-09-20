namespace UniConfig.Editor
{
    /// <summary>
    /// 支持的配置类型（枚举序值写入磁盘，只能在末尾追加，不可插入/重排）。
    /// </summary>
    public enum SupportableFieldType
    {
        Int16,
        Int32,
        Int64,
        UInt16,
        UInt32,
        UInt64,
        Single,
        Double,
        Boolean,
        String,

        Array1D_Int16,
        Array1D_Int32,
        Array1D_Int64,
        Array1D_UInt16,
        Array1D_UInt32,
        Array1D_UInt64,
        Array1D_Boolean,
        Array1D_Single,
        Array1D_Double,

        Array2D_Int16,
        Array2D_Int32,
        Array2D_Int64,
        Array2D_UInt16,
        Array2D_UInt32,
        Array2D_UInt64,
        Array2D_Boolean,
        Array2D_Single,
        Array2D_Double,

        Vector2,
        Vector3,
        Vector4,
        Quaternion,
        Vector2Int,
        Vector3Int,
        Color,
        Color32
    }
}
