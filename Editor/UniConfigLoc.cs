#region

using System.Collections.Generic;

#endregion

namespace UniConfig.Editor
{
    public enum UniConfigLanguage
    {
        Chinese = 0,
        English = 1
    }

    /// <summary>
    /// UniConfig 编辑器界面本地化（中文 / English）。
    /// </summary>
    public static class UniConfigLoc
    {
        private static readonly Dictionary<string, string[]> Table = new()
        {
            // 顶部工具栏
            ["load"] = new[] { "加载", "Load" },
            ["load.tip"] = new[] { "重新加载 Config 目录", "Reload Config folder" },
            ["save"] = new[] { "保存", "Save" },
            ["save_all.tip"] = new[] { "保存全部模块/表", "Save all modules/tables" },
            ["gen_cs"] = new[] { "生成C#", "Gen C#" },
            ["gen_cs.tip"] = new[] { "生成 Cfg.g.cs", "Generate Cfg.g.cs" },
            ["export"] = new[] { "导出配置", "Export" },
            ["export.tip"] = new[]
                { "按各模块导出器写出 configs.bytes（缺 C# 时会先生成）", "Write configs.bytes per exporter (auto Gen C# if needed)" },
            ["auto_save"] = new[] { "自动保存", "Auto Save" },
            ["auto_save.tip"] = new[] { "关闭窗口时自动保存一次", "Auto-save when closing the window" },
            ["settings"] = new[] { "设置", "Settings" },
            ["settings.tip"] = new[] { "打开 UniConfig 设置", "Open UniConfig settings" },
            ["unsaved"] = new[] { "*未保存", "*Unsaved" },
            ["language"] = new[] { "语言", "Language" },

            // 模块工具栏
            ["new_module"] = new[] { "新建", "New" },
            ["new_module.tip"] = new[] { "新建模块", "Create module" },
            ["add_table"] = new[] { "加表", "Add Table" },
            ["add_table.tip"] = new[] { "在当前模块下新建表", "Create table in current module" },
            ["rename"] = new[] { "重命名", "Rename" },
            ["rename_module.tip"] = new[] { "重命名当前模块", "Rename current module" },
            ["delete"] = new[] { "删除", "Delete" },
            ["delete_module.tip"] = new[] { "删除当前模块", "Delete current module" },
            ["exporter"] = new[] { "导出器", "Exporter" },
            ["no_exporter"] = new[] { "(无可用导出器)", "(No exporter)" },

            // 表工具栏
            ["export_type"] = new[] { "导出的类型", "Export type" },
            ["not_exported"] = new[] { "尚未导出", "Not exported" },
            ["rename_table.tip"] = new[] { "重命名当前表", "Rename current table" },
            ["delete_table.tip"] = new[] { "删除当前表", "Delete current table" },
            ["save_table.tip"] = new[] { "保存当前表", "Save current table" },
            ["add_field"] = new[] { "加字段", "Add Field" },
            ["add_field.tip"] = new[] { "添加字段", "Add field" },
            ["add_row"] = new[] { "加行", "Add Row" },
            ["add_row.tip"] = new[] { "添加行", "Add row" },
            ["search_cols"] = new[] { "搜索列", "Search Cols" },
            ["visible_cols"] = new[] { "显示列", "Visible Cols" },
            ["delete_row"] = new[] { "删除行", "Del Row" },
            ["delete_row.tip"] = new[] { "按行号删除（从 1 开始）", "Delete by row number (1-based)" },
            ["delete_col"] = new[] { "删除列", "Del Col" },
            ["delete_col.tip"] = new[] { "删除下拉框中勾选的列", "Delete checked columns" },
            ["type"] = new[] { "类型", "Type" },
            ["move_left"] = new[] { "左移列", "Move left" },
            ["move_right"] = new[] { "右移列", "Move right" },
            ["no_fields"] = new[] { "(无字段)", "(No fields)" },

            // 分页 / 搜索 / 加载
            ["page_info"] = new[]
                { "共 {0} 行{1}  ·  每页 {2}  ·  第 {3}/{4} 页", "Total {0} rows{1}  ·  {2}/page  ·  Page {3}/{4}" },
            ["match_count"] = new[] { "（匹配 {0}）", " (matched {0})" },
            ["searching_tag"] = new[] { "（搜索中…）", " (searching…)" },
            ["searching_help"] = new[]
            {
                "正在搜索{0}\n大数据量在后台处理，完成后自动刷新列表。",
                "Searching{0}\nLarge tables are processed in background; the list refreshes when done."
            },
            ["loading_help"] = new[]
            {
                "正在加载表数据：{0}/{1}{2}\n大表会在后台读取，编辑器不会卡死。",
                "Loading table: {0}/{1}{2}\nLarge tables load in background without freezing the editor."
            },
            ["select_hint"] = new[] { "请从左侧选择模块与配置表。", "Select a module and table from the left." },
            ["no_visible_cols"] = new[] { "当前没有可见列，请在「显示列」中勾选。", "No visible columns. Check some in Visible Cols." },

            // 状态栏
            ["ready"] = new[] { "就绪", "Ready" },
            ["module"] = new[] { "模块", "Module" },
            ["table"] = new[] { "表", "Table" },
            ["auto_save_on"] = new[] { "自动保存: 开", "Auto Save: On" },
            ["status.loaded_config"] = new[] { "已加载 Config 目录", "Config folder loaded" },
            ["status.saved_all"] = new[] { "已保存全部模块/表", "All modules/tables saved" },
            ["status.saved_all_skip"] = new[] { "已保存全部模块/表（跳过 {0} 行空数据）", "All saved (skipped {0} empty rows)" },
            ["status.generated"] = new[] { "已生成: {0}", "Generated: {0}" },
            ["status.exported"] = new[]
                { "已导出配置（若刚生成 C# 将在编译后自动继续）", "Exported (auto-continues after compile if C# was generated)" },
            ["status.exporter_updated"] = new[] { "模块导出器已更新: {0}", "Module exporter updated: {0}" },
            ["status.module_created"] = new[] { "已创建模块: {0}", "Module created: {0}" },
            ["status.table_created"] = new[] { "已创建表: {0}/{1}", "Table created: {0}/{1}" },
            ["status.module_deleted"] = new[] { "模块已删除", "Module deleted" },
            ["status.table_deleted"] = new[] { "表已删除", "Table deleted" },
            ["status.name_empty"] = new[] { "名称不能为空", "Name cannot be empty" },
            ["status.module_renamed"] = new[] { "模块已重命名为 {0}", "Module renamed to {0}" },
            ["status.table_renamed"] = new[] { "表已重命名为 {0}", "Table renamed to {0}" },
            ["status.loading"] = new[] { "正在加载 {0}/{1} ...", "Loading {0}/{1} ..." },
            ["status.loaded"] = new[] { "已加载 {0}/{1}（{2} 行）", "Loaded {0}/{1} ({2} rows)" },
            ["status.load_failed"] = new[] { "加载失败 {0}/{1}", "Load failed {0}/{1}" },
            ["status.saved_table"] = new[] { "已保存 {0}/{1}", "Saved {0}/{1}" },
            ["status.saved_table_skip"] =
                new[] { "已保存 {0}/{1}（跳过 {2} 行空数据）", "Saved {0}/{1} (skipped {2} empty rows)" },
            ["status.row_deleted"] = new[] { "已删除第 {0} 行", "Deleted row {0}" },
            ["status.cols_deleted"] = new[] { "已删除 {0} 列", "Deleted {0} column(s)" },
            ["status.searching"] = new[] { "正在搜索 {0}/{1} …", "Searching {0}/{1} …" },
            ["status.search_done"] = new[] { "搜索完成：匹配 {0} / {1} 行", "Search done: {0} / {1} rows matched" },

            // 对话框
            ["ok"] = new[] { "确定", "OK" },
            ["cancel"] = new[] { "取消", "Cancel" },
            ["select_module_first"] = new[] { "请先选择一个模块", "Select a module first" },
            ["delete_module.title"] = new[] { "删除模块", "Delete Module" },
            ["delete_module.msg"] = new[]
                { "确认删除模块「{0}」及其所有表？\n此操作不可撤销。", "Delete module \"{0}\" and all its tables?\nThis cannot be undone." },
            ["delete_table.title"] = new[] { "删除表", "Delete Table" },
            ["delete_table.msg"] = new[]
                { "确认删除表「{0}/{1}」？\n此操作不可撤销。", "Delete table \"{0}/{1}\"?\nThis cannot be undone." },
            ["delete_row.title"] = new[] { "删除行", "Delete Row" },
            ["delete_row.need_index"] = new[] { "请先输入要删除的行号（从 1 开始）。", "Enter a row number first (1-based)." },
            ["delete_row.need_int"] = new[] { "行号必须是整数。", "Row number must be an integer." },
            ["delete_row.empty"] = new[] { "当前表没有数据行。", "The table has no rows." },
            ["delete_row.invalid"] = new[] { "行号无效，请输入 1 ~ {0}。", "Invalid row number. Enter 1 ~ {0}." },
            ["delete_col.title"] = new[] { "删除列", "Delete Column" },
            ["delete_col.need_select"] = new[] { "请先在下拉框中勾选要删除的列。", "Check columns to delete in the dropdown first." },
            ["delete_col.none"] = new[] { "没有可删除的列。", "No columns to delete." },
            ["delete_col.confirm"] = new[] { "确认删除以下 {0} 列？\n{1}", "Delete the following {0} column(s)?\n{1}" },

            // 设置窗口
            ["settings.code_gen"] = new[] { "代码生成", "Code Generation" },
            ["settings.cs_dir"] = new[] { "Cfg.g.cs 导出目录", "Cfg.g.cs export folder" },
            ["settings.root_ns"] = new[] { "根命名空间", "Root namespace" },
            ["settings.export"] = new[] { "配置导出", "Config Export" },
            ["settings.res_dir"] = new[] { "Resources 导出目录", "Resources export folder" },
            ["settings.sa_dir"] = new[] { "StreamingAssets 导出目录", "StreamingAssets export folder" },
            ["settings.export_file_hint"] = new[]
            {
                "文件名固定为 configs.bytes。Resources 加载时请去掉扩展名（例如目录 Assets/Resources → 加载路径 \"configs\"）。",
                "File name is always configs.bytes. For Resources.Load, omit the extension (e.g. Assets/Resources → load path \"configs\")."
            },
            ["settings.editor"] = new[] { "编辑器", "Editor" },
            ["settings.auto_save_close"] = new[] { "关闭时自动保存", "Auto-save on close" },
            ["settings.open_editor"] = new[] { "打开配置编辑器", "Open Config Editor" },

            // 约束
            ["constraint.open"] = new[] { "添加约束", "Constraints" },
            ["constraint.open.tip"] = new[] { "打开字段约束编辑窗口（外键 / 公式）", "Open field constraints (FK / formula)" },
            ["constraint.table_missing"] = new[] { "表已不存在，请关闭此窗口。", "Table no longer exists. Close this window." },
            ["constraint.need_load"] = new[] { "请先加载表数据后再编辑约束。", "Load table data before editing constraints." },
            ["constraint.load_now"] = new[] { "立即加载", "Load Now" },
            ["constraint.add"] = new[] { "新建约束", "Add" },
            ["constraint.remove"] = new[] { "删除约束", "Remove" },
            ["constraint.validate_fk"] = new[] { "校验外键", "Validate FK" },
            ["constraint.recalc_all"] = new[] { "全部重算", "Recalc All" },
            ["constraint.save"] = new[] { "保存", "Save" },
            ["constraint.formula_hint"] = new[]
            {
                "在表达式中直接书写：本表 {attack}；跨表/跨模块 {Test2Module/Skill.attack}。跨表默认用双方 id 对齐。\n公式字段在表格中不可编辑，依赖字段变化时会自动重算；表达式合法后会自动全表计算一次。",
                "Write directly in the expression: local {attack}; cross {Module/Table.attack}. Cross-table rows match by id.\nFormula fields are read-only in the grid and recalculate when dependencies change; a full apply runs once when the formula becomes valid."
            },
            ["constraint.formula_ok"] = new[] { "表达式合法。", "Expression is valid." },
            ["constraint.save_blocked"] = new[]
                { "存在不合法的公式约束，已阻止保存。请先修正。", "Invalid formula constraint(s). Fix them before saving." },
            ["constraint.recalc_warn"] = new[]
                { "已更新 {0} 个单元格，另有 {1} 条警告。", "Updated {0} cell(s), with {1} warning(s)." },
            ["constraint.err_fk_incomplete"] = new[] { "字段 {0} 的外键配置不完整。", "FK config incomplete for field {0}." },
            ["constraint.err_fk_table"] = new[] { "字段 {0}：找不到引用表 {1}/{2}。", "Field {0}: ref table {1}/{2} not found." },
            ["constraint.err_fk_value"] = new[]
                { "第 {0} 行 {1}='{2}' 不在 {3}/{4}.{5} 中。", "Row {0} {1}='{2}' not in {3}/{4}.{5}." },
            ["constraint.err_no_target"] = new[] { "未选择目标字段。", "Target field is empty." },
            ["constraint.err_target_missing"] = new[] { "目标字段 {0} 不存在。", "Target field {0} does not exist." },
            ["constraint.err_empty_expr"] = new[] { "字段 {0} 的公式表达式为空。", "Formula for {0} is empty." },
            ["constraint.err_empty_token"] = new[] { "字段 {0} 的公式包含空的 {{}}。", "Formula for {0} contains empty {{}}." },
            ["constraint.err_self_ref"] = new[] { "字段 {0} 的公式不能引用自身。", "Formula for {0} cannot reference itself." },
            ["constraint.err_local_field"] = new[] { "字段 {0}：本表不存在字段 {1}。", "Field {0}: local field {1} not found." },
            ["constraint.err_cross_table"] = new[]
                { "字段 {0}：找不到跨表 {1}/{2}。", "Field {0}: cross table {1}/{2} not found." },
            ["constraint.err_cross_field"] = new[]
                { "字段 {0}：跨表引用 {1} 的字段不存在。", "Field {0}: cross ref {1} field missing." },
            ["constraint.err_expr_parse"] = new[] { "字段 {0} 表达式无法解析：{1}", "Field {0} expression error: {1}" },
            ["constraint.err_row"] = new[] { "第 {0} 行字段 {1}：{2}", "Row {0} field {1}: {2}" },
            ["constraint.list"] = new[] { "约束列表", "Constraints" },
            ["constraint.detail"] = new[] { "约束详情", "Details" },
            ["constraint.empty"] = new[] { "暂无约束，点击「新建约束」添加。", "No constraints yet. Click Add." },
            ["constraint.select_one"] = new[] { "请选择左侧一条约束进行编辑。", "Select a constraint on the left." },
            ["constraint.no_fields"] = new[] { "当前表没有字段。", "This table has no fields." },
            ["constraint.no_modules"] = new[] { "没有可用模块。", "No modules available." },
            ["constraint.no_tables"] = new[] { "该模块下没有表。", "No tables in this module." },
            ["constraint.fk_need_fields"] = new[]
            {
                "引用表暂无字段定义，可手动填写字段名，或先打开该表加载。", "Ref table has no fields yet. Type field name or load that table first."
            },
            ["constraint.target_field"] = new[] { "目标字段", "Target Field" },
            ["constraint.kind"] = new[] { "约束类型", "Type" },
            ["constraint.kind_fk"] = new[] { "外键", "FK" },
            ["constraint.kind_formula"] = new[] { "公式", "Formula" },
            ["constraint.fk_module"] = new[] { "引用模块", "Ref Module" },
            ["constraint.fk_table"] = new[] { "引用表", "Ref Table" },
            ["constraint.fk_field"] = new[] { "引用字段", "Ref Field" },
            ["constraint.expression"] = new[] { "表达式", "Expression" },
            ["constraint.added"] = new[] { "已添加约束", "Constraint added" },
            ["constraint.removed"] = new[] { "已删除约束", "Constraint removed" },
            ["constraint.fk_ok"] = new[] { "外键校验通过", "Foreign key validation passed" },
            ["constraint.fk_fail"] = new[] { "校验发现问题：{0} 处", "Validation found {0} issue(s)" },
            ["constraint.fk_invalid_status"] = new[] { "外键约束不合法", "Foreign key constraint invalid" },
            ["constraint.fk_detail_value"] = new[]
            {
                "外键不合法：第 {0} 行字段 {1}='{2}' 是 {3}/{4}.{5} 的外键，引用表中不存在该值",
                "FK invalid: row {0} field {1}='{2}' references {3}/{4}.{5}, value not found in ref table"
            },
            ["constraint.fk_detail_missing_table"] = new[]
            {
                "外键不合法：字段 {0} 引用 {1}/{2}.{3}，但引用表不存在",
                "FK invalid: field {0} references {1}/{2}.{3}, but ref table is missing"
            },
            ["constraint.fk_detail_incomplete"] = new[]
            {
                "外键不合法：字段 {0} 的外键配置不完整（{1}）",
                "FK invalid: field {0} has incomplete FK config ({1})"
            },
            ["constraint.fk_detail_more"] = new[]
            {
                "{0}（共 {1} 处）",
                "{0} ({1} issues)"
            },
            ["constraint.formula_done"] = new[] { "公式已计算，更新 {0} 个单元格", "Formula computed, {0} cell(s) updated" },
            ["constraint.help"] = new[]
            {
                "外键：目标字段值必须存在于引用表指定列，可用「校验外键」检查。\n公式：在 {{}} 中写本表字段或 模块/表.字段，如 ({attack}-{defend})*2 或 {value}+{Test2Module/Skill.attack}。\n有公式约束的字段在表里不可编辑；改依赖字段会自动重算。表达式合法后自动全表算一遍。「全部重算」可手动再算。最后点「保存」写入文件。",
                "FK: values must exist in the referenced column; use Validate FK.\nFormula: write {field} or {Module/Table.field} inside braces, e.g. ({attack}-{defend})*2.\nFormula fields are read-only; changing dependencies auto-recalculates. A full apply runs when the formula becomes valid. Use Recalc All anytime. Click Save to persist."
            }
        };

        public static UniConfigLanguage Language
        {
            get
            {
                int v = UniConfigEditorSettings.instance.editorLanguage;
                return v == (int)UniConfigLanguage.English
                    ? UniConfigLanguage.English
                    : UniConfigLanguage.Chinese;
            }
            set
            {
                UniConfigEditorSettings.instance.editorLanguage = (int)value;
                UniConfigEditorSettings.instance.SaveSettings();
            }
        }

        public static bool IsChinese => Language == UniConfigLanguage.Chinese;

        public static string T(string key)
        {
            if (Table.TryGetValue(key, out string[] pair) && pair != null && pair.Length >= 2)
                return IsChinese ? pair[0] : pair[1];
            return key;
        }

        public static string F(string key, params object[] args)
        {
            return string.Format(T(key), args);
        }
    }
}