using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Web.Script.Serialization;

namespace FfxivKoreanPatch.FFXIVPatchGenerator
{
    internal sealed class PatchPolicy
    {
        public static readonly PatchPolicy Empty = new PatchPolicy();
        private static readonly uint[] CompactTimeUnitAddonRows = new uint[] { 44, 45, 49 };
        private static readonly uint[] EnglishCompactDurationAddonRows = new uint[] { 2338, 6166 };

        private readonly HashSet<string> _deleteFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PatchSheetPolicy> _sheets = new Dictionary<string, PatchSheetPolicy>(StringComparer.OrdinalIgnoreCase);
        private readonly List<Regex> _rowKeyFallbackFiles = new List<Regex>();

        public static PatchPolicy Load(string policyPath)
        {
            PatchPolicy policy = CreateBuiltInDefault();
            if (string.IsNullOrWhiteSpace(policyPath))
            {
                return policy;
            }

            string fullPath = Path.GetFullPath(policyPath);
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException("Patch policy file was not found.", fullPath);
            }

            string json = File.ReadAllText(fullPath, new UTF8Encoding(false));
            JavaScriptSerializer serializer = new JavaScriptSerializer();
            Dictionary<string, object> root = serializer.DeserializeObject(json) as Dictionary<string, object>;
            if (root == null)
            {
                throw new InvalidDataException("Patch policy root must be a JSON object: " + fullPath);
            }

            policy.LoadDeleteFiles(root);
            policy.LoadPatternList(root, "row_key_fallback_files", policy._rowKeyFallbackFiles);
            policy.LoadRowLists(root, "keep_rows", delegate(PatchSheetPolicy sheetPolicy, uint rowId) { sheetPolicy.KeepRows.Add(rowId); });
            policy.LoadRowLists(root, "delete_rows", delegate(PatchSheetPolicy sheetPolicy, uint rowId) { sheetPolicy.KeepRows.Add(rowId); });
            policy.LoadColumnLists(root, "keep_columns", delegate(PatchSheetPolicy sheetPolicy, ushort columnOffset) { sheetPolicy.KeepColumns.Add(columnOffset); });
            policy.LoadColumnLists(root, "delete_columns", delegate(PatchSheetPolicy sheetPolicy, ushort columnOffset) { sheetPolicy.KeepColumns.Add(columnOffset); });
            policy.LoadRemapKeys(root);
            policy.LoadRemapColumns(root);
            return policy;
        }

        private static PatchPolicy CreateBuiltInDefault()
        {
            PatchPolicy policy = new PatchPolicy();
            PatchSheetPolicy addonPolicy = policy.GetOrCreateSheetPolicy("Addon");

            // Global Addon rows 44/45/49 are compact h/m/s time-unit labels.
            // Korean "시간/분/초" overflows narrow global UI slots such as icon timers.
            for (int i = 0; i < CompactTimeUnitAddonRows.Length; i++)
            {
                addonPolicy.KeepRows.Add(CompactTimeUnitAddonRows[i]);
            }

            // Addon row 10952 is the party-list self marker. It is a private glyph
            // token in the original client, but Korean font replacement can render it
            // as "=". Use plain ASCII here to avoid touching global font glyph tables.
            addonPolicy.SetRowColumnRemap(10952, 0, ColumnRemap.Literal("1"));

            // Some short duration templates embed h/m labels inside SeString branches.
            // Rewriting the Korean bytes in-place would require recalculating SeString
            // branch lengths, so use the global English templates for these rows.
            for (int i = 0; i < EnglishCompactDurationAddonRows.Length; i++)
            {
                addonPolicy.GlobalEnglishRows.Add(EnglishCompactDurationAddonRows[i]);
            }

            return policy;
        }

        public bool ShouldSkipSheet(string sheetName)
        {
            return _deleteFiles.Contains(NormalizeSheetKey(sheetName)) ||
                   _deleteFiles.Contains(NormalizeSheetKey(sheetName) + ".csv");
        }

        public bool ShouldAllowRowKeyFallback(string sheetName)
        {
            string key = NormalizeSheetKey(sheetName);
            for (int i = 0; i < _rowKeyFallbackFiles.Count; i++)
            {
                if (_rowKeyFallbackFiles[i].IsMatch(key))
                {
                    return true;
                }
            }

            return false;
        }

        public PatchSheetPolicy GetSheetPolicy(string sheetName)
        {
            PatchSheetPolicy sheetPolicy;
            if (_sheets.TryGetValue(NormalizeSheetKey(sheetName), out sheetPolicy))
            {
                return sheetPolicy;
            }

            if (_sheets.TryGetValue(NormalizeSheetKey(sheetName) + ".csv", out sheetPolicy))
            {
                return sheetPolicy;
            }

            return PatchSheetPolicy.Empty;
        }

        private void LoadDeleteFiles(Dictionary<string, object> root)
        {
            object value;
            if (!root.TryGetValue("delete_files", out value))
            {
                return;
            }

            foreach (object item in EnumerateList(value))
            {
                string key = NormalizeSheetKey(Convert.ToString(item));
                if (!string.IsNullOrEmpty(key))
                {
                    _deleteFiles.Add(key);
                }
            }
        }

        private void LoadPatternList(Dictionary<string, object> root, string fieldName, List<Regex> target)
        {
            object value;
            if (!root.TryGetValue(fieldName, out value))
            {
                return;
            }

            foreach (object item in EnumerateList(value))
            {
                string key = NormalizeSheetKey(Convert.ToString(item));
                if (!string.IsNullOrEmpty(key))
                {
                    target.Add(CreateSheetMatcher(key));
                }
            }
        }

        private void LoadRowLists(Dictionary<string, object> root, string fieldName, Action<PatchSheetPolicy, uint> add)
        {
            object value;
            Dictionary<string, object> map;
            if (!root.TryGetValue(fieldName, out value) || (map = value as Dictionary<string, object>) == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> fileEntry in map)
            {
                PatchSheetPolicy sheetPolicy = GetOrCreateSheetPolicy(fileEntry.Key);
                foreach (object rowValue in EnumerateList(fileEntry.Value))
                {
                    uint rowId;
                    if (TryParseUInt(rowValue, out rowId))
                    {
                        add(sheetPolicy, rowId);
                    }
                }
            }
        }

        private void LoadColumnLists(Dictionary<string, object> root, string fieldName, Action<PatchSheetPolicy, ushort> add)
        {
            object value;
            Dictionary<string, object> map;
            if (!root.TryGetValue(fieldName, out value) || (map = value as Dictionary<string, object>) == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> fileEntry in map)
            {
                PatchSheetPolicy sheetPolicy = GetOrCreateSheetPolicy(fileEntry.Key);
                foreach (object columnValue in EnumerateList(fileEntry.Value))
                {
                    ushort columnOffset;
                    if (TryParseUShort(columnValue, out columnOffset))
                    {
                        add(sheetPolicy, columnOffset);
                    }
                }
            }
        }

        private void LoadRemapKeys(Dictionary<string, object> root)
        {
            object value;
            Dictionary<string, object> map;
            if (!root.TryGetValue("remap_keys", out value) || (map = value as Dictionary<string, object>) == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> fileEntry in map)
            {
                Dictionary<string, object> rows = fileEntry.Value as Dictionary<string, object>;
                if (rows == null)
                {
                    continue;
                }

                PatchSheetPolicy sheetPolicy = GetOrCreateSheetPolicy(fileEntry.Key);
                foreach (KeyValuePair<string, object> rowEntry in rows)
                {
                    uint targetRowId;
                    uint sourceRowId;
                    if (TryParseUInt(rowEntry.Key, out targetRowId) && TryParseUInt(rowEntry.Value, out sourceRowId))
                    {
                        sheetPolicy.SourceRowOverrides[targetRowId] = sourceRowId;
                    }
                }
            }
        }

        private void LoadRemapColumns(Dictionary<string, object> root)
        {
            object value;
            Dictionary<string, object> map;
            if (!root.TryGetValue("remap_columns", out value) || (map = value as Dictionary<string, object>) == null)
            {
                return;
            }

            foreach (KeyValuePair<string, object> fileEntry in map)
            {
                Dictionary<string, object> columns = fileEntry.Value as Dictionary<string, object>;
                if (columns == null)
                {
                    continue;
                }

                PatchSheetPolicy sheetPolicy = GetOrCreateSheetPolicy(fileEntry.Key);
                foreach (KeyValuePair<string, object> columnEntry in columns)
                {
                    ushort columnOffset;
                    if (!TryParseUShort(columnEntry.Key, out columnOffset))
                    {
                        continue;
                    }

                    Dictionary<string, object> rowMap = columnEntry.Value as Dictionary<string, object>;
                    if (rowMap == null)
                    {
                        sheetPolicy.ColumnRemaps[columnOffset] = ColumnRemap.Parse(columnEntry.Value);
                        continue;
                    }

                    Dictionary<uint, ColumnRemap> remapsByRow;
                    if (!sheetPolicy.RowColumnRemaps.TryGetValue(columnOffset, out remapsByRow))
                    {
                        remapsByRow = new Dictionary<uint, ColumnRemap>();
                        sheetPolicy.RowColumnRemaps[columnOffset] = remapsByRow;
                    }

                    foreach (KeyValuePair<string, object> rowEntry in rowMap)
                    {
                        uint rowId;
                        if (TryParseUInt(rowEntry.Key, out rowId))
                        {
                            remapsByRow[rowId] = ColumnRemap.Parse(rowEntry.Value);
                        }
                    }
                }
            }
        }

        private PatchSheetPolicy GetOrCreateSheetPolicy(string sheetName)
        {
            string key = NormalizeSheetKey(sheetName);
            PatchSheetPolicy sheetPolicy;
            if (!_sheets.TryGetValue(key, out sheetPolicy))
            {
                sheetPolicy = new PatchSheetPolicy();
                _sheets[key] = sheetPolicy;
            }

            return sheetPolicy;
        }

        private static IEnumerable<object> EnumerateList(object value)
        {
            if (value == null)
            {
                yield break;
            }

            string text = value as string;
            if (text != null)
            {
                yield return text;
                yield break;
            }

            IEnumerable enumerable = value as IEnumerable;
            if (enumerable == null)
            {
                yield return value;
                yield break;
            }

            foreach (object item in enumerable)
            {
                yield return item;
            }
        }

        private static bool TryParseUInt(object value, out uint result)
        {
            if (value is int)
            {
                int intValue = (int)value;
                if (intValue >= 0)
                {
                    result = (uint)intValue;
                    return true;
                }
            }

            if (value is long)
            {
                long longValue = (long)value;
                if (longValue >= 0 && longValue <= uint.MaxValue)
                {
                    result = (uint)longValue;
                    return true;
                }
            }

            return uint.TryParse(Convert.ToString(value), out result);
        }

        private static bool TryParseUShort(object value, out ushort result)
        {
            uint uintValue;
            if (TryParseUInt(value, out uintValue) && uintValue <= ushort.MaxValue)
            {
                result = (ushort)uintValue;
                return true;
            }

            result = 0;
            return false;
        }

        private static string NormalizeSheetKey(string value)
        {
            string key = (value ?? string.Empty).Trim().Replace('\\', '/').ToLowerInvariant();
            if (key.StartsWith("exd/", StringComparison.OrdinalIgnoreCase))
            {
                key = key.Substring(4);
            }

            if (key.EndsWith(".exh", StringComparison.OrdinalIgnoreCase))
            {
                key = key.Substring(0, key.Length - 4);
            }

            if (key.EndsWith(".exd", StringComparison.OrdinalIgnoreCase))
            {
                key = key.Substring(0, key.Length - 4);
            }

            return key;
        }

        private static Regex CreateSheetMatcher(string key)
        {
            string pattern = "^" + Regex.Escape(key).Replace("\\*", ".*").Replace("\\?", ".") + "$";
            return new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }

    internal sealed class PatchSheetPolicy
    {
        public static readonly PatchSheetPolicy Empty = new PatchSheetPolicy(true);

        public readonly HashSet<uint> KeepRows = new HashSet<uint>();
        public readonly HashSet<ushort> KeepColumns = new HashSet<ushort>();
        public readonly Dictionary<uint, uint> SourceRowOverrides = new Dictionary<uint, uint>();
        public readonly Dictionary<ushort, ColumnRemap> ColumnRemaps = new Dictionary<ushort, ColumnRemap>();
        public readonly Dictionary<ushort, Dictionary<uint, ColumnRemap>> RowColumnRemaps = new Dictionary<ushort, Dictionary<uint, ColumnRemap>>();
        public readonly HashSet<uint> GlobalEnglishRows = new HashSet<uint>();

        private readonly bool _readOnly;

        public PatchSheetPolicy()
        {
        }

        private PatchSheetPolicy(bool readOnly)
        {
            _readOnly = readOnly;
        }

        public bool HasSourceRowOverrides
        {
            get { return SourceRowOverrides.Count > 0; }
        }

        public bool HasGlobalEnglishRows
        {
            get { return GlobalEnglishRows.Count > 0; }
        }

        public bool IsReadOnly
        {
            get { return _readOnly; }
        }

        public bool ShouldKeepRow(uint rowId)
        {
            return KeepRows.Contains(rowId);
        }

        public bool ShouldKeepColumn(uint rowId, ushort columnOffset)
        {
            return KeepColumns.Contains(columnOffset) || GetColumnRemap(rowId, columnOffset).Mode == ColumnRemapMode.KeepGlobal;
        }

        public ColumnRemap GetColumnRemap(uint rowId, ushort columnOffset)
        {
            Dictionary<uint, ColumnRemap> remapsByRow;
            ColumnRemap remap;
            if (RowColumnRemaps.TryGetValue(columnOffset, out remapsByRow) &&
                remapsByRow.TryGetValue(rowId, out remap))
            {
                return remap;
            }

            if (ColumnRemaps.TryGetValue(columnOffset, out remap))
            {
                return remap;
            }

            return ColumnRemap.Default;
        }

        public void SetRowColumnRemap(uint rowId, ushort columnOffset, ColumnRemap remap)
        {
            if (_readOnly)
            {
                throw new InvalidOperationException("Cannot mutate read-only patch policy.");
            }

            Dictionary<uint, ColumnRemap> remapsByRow;
            if (!RowColumnRemaps.TryGetValue(columnOffset, out remapsByRow))
            {
                remapsByRow = new Dictionary<uint, ColumnRemap>();
                RowColumnRemaps[columnOffset] = remapsByRow;
            }

            remapsByRow[rowId] = remap;
        }
    }

    internal struct ColumnRemap
    {
        public static readonly ColumnRemap Default = new ColumnRemap(ColumnRemapMode.Default, null, null);
        public static readonly ColumnRemap KeepGlobal = new ColumnRemap(ColumnRemapMode.KeepGlobal, null, null);

        public readonly ColumnRemapMode Mode;
        public readonly ushort? SourceColumnOffset;
        public readonly byte[] LiteralBytes;

        private ColumnRemap(ColumnRemapMode mode, ushort? sourceColumnOffset, byte[] literalBytes)
        {
            Mode = mode;
            SourceColumnOffset = sourceColumnOffset;
            LiteralBytes = literalBytes;
        }

        public static ColumnRemap SourceColumn(ushort sourceColumnOffset)
        {
            return new ColumnRemap(ColumnRemapMode.SourceColumn, sourceColumnOffset, null);
        }

        public static ColumnRemap Literal(string value)
        {
            return new ColumnRemap(ColumnRemapMode.Literal, null, new UTF8Encoding(false).GetBytes(value ?? string.Empty));
        }

        public static ColumnRemap Parse(object value)
        {
            string text = Convert.ToString(value);
            if (string.Equals(text, "G", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "GLOBAL", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(text, "KEEP", StringComparison.OrdinalIgnoreCase))
            {
                return KeepGlobal;
            }

            ushort sourceColumnOffset;
            if (ushort.TryParse(text, out sourceColumnOffset))
            {
                return SourceColumn(sourceColumnOffset);
            }

            return Literal(text);
        }
    }

    internal enum ColumnRemapMode
    {
        Default,
        KeepGlobal,
        SourceColumn,
        Literal
    }
}
