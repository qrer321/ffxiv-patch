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
        private static readonly uint[] EnglishCompactDurationAddonRows = new uint[] { 876, 2338, 6166 };
        private static readonly KeyValuePair<uint, string>[] CompactMinutePresetAddonRows = new KeyValuePair<uint, string>[]
        {
            new KeyValuePair<uint, string>(8291, "5m"),
            new KeyValuePair<uint, string>(8292, "10m"),
            new KeyValuePair<uint, string>(8293, "30m"),
            new KeyValuePair<uint, string>(8294, "60m")
        };
        private static readonly uint[] ConfigShareAddonTitleRows = new uint[] { 17300, 17301 };
        private const uint MkdSupportJobFirstRow = 0;
        private const uint MkdSupportJobLastPlayableRow = 15;
        private const ushort MkdSupportJobFullNameColumnOffset = 0;
        private const ushort MkdSupportJobShortNameColumnOffset = 4;
        private const ushort MkdSupportJobEnglishFullNameColumnOffset = 16;
        private static readonly RowRange[] GlobalLobbyDataCenterRowRanges = new RowRange[]
        {
            new RowRange(791, 794),
            new RowRange(800, 806),
            new RowRange(808, 816)
        };
        private static readonly uint[] GlobalDataCenterTravelAddonRows = new uint[] { 12514, 12525 };

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
            policy.LoadRowLists(root, "delete_rows", delegate(PatchSheetPolicy sheetPolicy, uint rowId) { sheetPolicy.KeepRows.Add(rowId); }); // legacy alias: delete from patch output, keep global source row
            policy.LoadColumnLists(root, "keep_columns", delegate(PatchSheetPolicy sheetPolicy, ushort columnOffset) { sheetPolicy.KeepColumns.Add(columnOffset); });
            policy.LoadColumnLists(root, "delete_columns", delegate(PatchSheetPolicy sheetPolicy, ushort columnOffset) { sheetPolicy.KeepColumns.Add(columnOffset); }); // legacy alias: delete from patch output, keep global source column
            policy.LoadRowLists(root, "global_english_rows", delegate(PatchSheetPolicy sheetPolicy, uint rowId) { sheetPolicy.GlobalEnglishRows.Add(rowId); });
            policy.LoadRowLists(root, "global_target_rows", delegate(PatchSheetPolicy sheetPolicy, uint rowId) { sheetPolicy.GlobalTargetRows.Add(rowId); });
            policy.LoadRemapKeys(root);
            policy.LoadRemapColumns(root);
            return policy;
        }

        private static PatchPolicy CreateBuiltInDefault()
        {
            PatchPolicy policy = new PatchPolicy();
            PatchSheetPolicy addonPolicy = policy.GetOrCreateSheetPolicy("Addon");
            PatchSheetPolicy lobbyPolicy = policy.GetOrCreateSheetPolicy("Lobby");
            PatchSheetPolicy mkdSupportJobPolicy = policy.GetOrCreateSheetPolicy("MkdSupportJob");
            PatchSheetPolicy worldDcGroupTypePolicy = policy.GetOrCreateSheetPolicy("WorldDCGroupType");
            PatchSheetPolicy worldPhysicalDcPolicy = policy.GetOrCreateSheetPolicy("WorldPhysicalDC");
            PatchSheetPolicy worldRegionGroupPolicy = policy.GetOrCreateSheetPolicy("WorldRegionGroup");

            // Global Addon rows 44/45/49 are compact h/m/s time-unit labels.
            // Korean "시간/분/초" overflows narrow global UI slots such as icon timers.
            for (int i = 0; i < CompactTimeUnitAddonRows.Length; i++)
            {
                addonPolicy.KeepRows.Add(CompactTimeUnitAddonRows[i]);
            }

            // Addon row 10952 is the party-list self marker. It is a private glyph
            // token in the Japanese client. Keep the target U+E031 token and let
            // the font patch copy clean boxed marker pixels into that glyph range.
            addonPolicy.GlobalTargetRows.Add(10952);

            // The data-center selection screen is global-client-only lobby UI.
            // Keep the selected global client language so labels such as
            // INFORMATION and region messages do not fall back through Korean
            // proxy glyphs in this font route.
            AddRows(lobbyPolicy.GlobalTargetRows, GlobalLobbyDataCenterRowRanges);

            // Most Addon rows in this range are the normal World Visit UI and have
            // Korean source text. Keep only known global-client-only rows global;
            // otherwise labels such as "World Visit" stay untranslated.
            for (int i = 0; i < GlobalDataCenterTravelAddonRows.Length; i++)
            {
                addonPolicy.GlobalTargetRows.Add(GlobalDataCenterTravelAddonRows[i]);
            }

            // Global row 12511 is "World Visit", but the Korean row 12511 is empty.
            // Korean rows 12510/12524/12537 carry the intended "server teleport"
            // wording, so map the title row to row 12524 instead of leaving it
            // Japanese.
            addonPolicy.SourceRowOverrides[12511] = 12524;

            // Some short duration templates embed compact h/m labels or glyphs inside
            // SeString branches. Rewriting the Korean bytes in-place would require
            // recalculating SeString branch lengths, so use the global English
            // templates for these rows.
            for (int i = 0; i < EnglishCompactDurationAddonRows.Length; i++)
            {
                addonPolicy.GlobalEnglishRows.Add(EnglishCompactDurationAddonRows[i]);
            }

            // These preset timer labels are used in compact status/buff UI. They
            // are plain strings, not SeString branch templates, so pin them to the
            // global h/m style directly instead of leaving Korean "분" in narrow UI.
            for (int i = 0; i < CompactMinutePresetAddonRows.Length; i++)
            {
                KeyValuePair<uint, string> preset = CompactMinutePresetAddonRows[i];
                addonPolicy.GlobalTargetRows.Add(preset.Key);
                addonPolicy.SetRowColumnRemap(preset.Key, 0, ColumnRemap.Literal(preset.Value));
            }

            // The Configuration Sharing window title lives in Addon, not
            // MainCommand. Keep both nearby title rows pinned to Korean because
            // some global clients route the visible label through Addon#17300.
            for (int i = 0; i < ConfigShareAddonTitleRows.Length; i++)
            {
                addonPolicy.SetRowColumnRemap(ConfigShareAddonTitleRows[i], 0, ColumnRemap.Literal("\uC124\uC815 \uACF5\uC720"));
            }

            // Occult Crescent HUDs consume MkdSupportJob name columns in multiple
            // places. Columns 0 and 4 are the full/short main phantom-job labels
            // used by HUD variants; descriptive/support-action text remains
            // translated.
            for (uint rowId = MkdSupportJobFirstRow; rowId <= MkdSupportJobLastPlayableRow; rowId++)
            {
                mkdSupportJobPolicy.SetRowColumnRemap(
                    rowId,
                    MkdSupportJobFullNameColumnOffset,
                    ColumnRemap.SourceColumn(MkdSupportJobEnglishFullNameColumnOffset));
                mkdSupportJobPolicy.SetRowColumnRemap(
                    rowId,
                    MkdSupportJobShortNameColumnOffset,
                    ColumnRemap.KeepGlobal);
            }

            // Region labels such as Japan/North America are part of the same lobby
            // flow. Keep the selected global language for these lookup sheets too.
            for (uint rowId = 1; rowId <= 8; rowId++)
            {
                worldRegionGroupPolicy.GlobalTargetRows.Add(rowId);
            }

            for (uint rowId = 1; rowId <= 32; rowId++)
            {
                worldDcGroupTypePolicy.GlobalTargetRows.Add(rowId);
            }

            for (uint rowId = 1; rowId <= 8; rowId++)
            {
                worldPhysicalDcPolicy.GlobalTargetRows.Add(rowId);
            }

            return policy;
        }

        private static void AddRows(HashSet<uint> target, RowRange[] ranges)
        {
            for (int i = 0; i < ranges.Length; i++)
            {
                RowRange range = ranges[i];
                for (uint rowId = range.First; rowId <= range.Last; rowId++)
                {
                    target.Add(rowId);
                    if (rowId == uint.MaxValue)
                    {
                        break;
                    }
                }
            }
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

    internal struct RowRange
    {
        public readonly uint First;
        public readonly uint Last;

        public RowRange(uint first, uint last)
        {
            if (last < first)
            {
                throw new ArgumentOutOfRangeException("last", "Range end must be greater than or equal to range start.");
            }

            First = first;
            Last = last;
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
        public readonly HashSet<uint> GlobalTargetRows = new HashSet<uint>();

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

        public bool HasGlobalTargetRows
        {
            get { return GlobalTargetRows.Count > 0; }
        }

        public bool IsReadOnly
        {
            get { return _readOnly; }
        }

        public bool ShouldKeepRow(uint rowId)
        {
            return KeepRows.Contains(rowId);
        }

        public bool ShouldUseGlobalFallbackRow(uint rowId)
        {
            return GlobalEnglishRows.Contains(rowId) || GlobalTargetRows.Contains(rowId);
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
