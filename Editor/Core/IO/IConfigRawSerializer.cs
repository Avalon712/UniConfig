namespace UniConfig.Editor
{
    internal interface IConfigRawSerializer
    {
        void SaveTable(ConfigTable table);

        ConfigTable LoadTable(string moduleName, string tableName);
    }
}