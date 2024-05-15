using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using EFT.UI;
using InGameMap.Data;
using InGameMap.Utils;
using UnityEngine;

namespace InGameMap.UI.Controls
{
    public class MapSelectDropdown : MonoBehaviour
    {
        private static HashSet<string> _acceptableExtensions = new HashSet<string>{ "json", "jsonc" };

        public event Action<MapDef> OnMapSelected;
        public RectTransform RectTransform => gameObject.transform as RectTransform;

        private DropDownBox _dropdown;
        private Dictionary<string, DateTime> _writeTimes = new Dictionary<string, DateTime>();
        private Dictionary<string, MapDef> _mapDefs = new Dictionary<string, MapDef>();
        private List<MapDef> _selectableMapDefs;
        private string _nameFilter = null;

        public static MapSelectDropdown Create(GameObject prefab, Transform parent, Vector2 position, Vector2 size)
        {
            var go = GameObject.Instantiate(prefab);
            go.name = "MapSelectDropdown";

            var rectTransform = go.GetRectTransform();
            rectTransform.SetParent(parent);
            rectTransform.localScale = Vector3.one;
            rectTransform.sizeDelta = size;
            rectTransform.anchoredPosition = position; // this is lazy, prob should adjust all of the anchors

            var dropdown = go.AddComponent<MapSelectDropdown>();
            return dropdown;
        }

        private void Awake()
        {
            _dropdown = gameObject.GetComponentInChildren<DropDownBox>();
            _dropdown.SetLabelText("Select a Map");
        }

        private void OnSelectDropdownMap(int index)
        {
            OnMapSelected?.Invoke(_selectableMapDefs[index]);
        }

        private void ChangeAvailableMapDefs(IEnumerable<MapDef> mapDefs)
        {
            // check if these are the same map defs as before
            if (_selectableMapDefs != null && Enumerable.SequenceEqual(mapDefs, _selectableMapDefs))
            {
                return;
            }

            _selectableMapDefs = mapDefs.ToList();

            _dropdown.Show(_selectableMapDefs.Select(def => def.DisplayName));

            // TODO: this references a BSG method of GInterface390
            _dropdown.OnValueChanged.Bind(OnSelectDropdownMap, 0);

            gameObject.SetActive(_selectableMapDefs.Count > 1);
        }

        public void LoadMapDefsFromPath(string relPath)
        {
            var absolutePath = Path.Combine(Plugin.Path, relPath);
            var paths = Directory.EnumerateFiles(absolutePath, "*.*", SearchOption.AllDirectories)
                .Where(p => _acceptableExtensions.Contains(Path.GetExtension(p).TrimStart('.').ToLowerInvariant()));

            // NOTE: this will not catch deleted maps, which I'm fine with
            foreach (var path in paths)
            {
                var writeTime = File.GetLastWriteTimeUtc(path);
                if (_writeTimes.ContainsKey(path) && _writeTimes[path] == writeTime)
                {
                    // this is the same file as before
                    continue;
                }

                _writeTimes[path] = writeTime;

                Plugin.Log.LogInfo($"Trying to load MapDef from path: {path}");
                var mapDef = MapDef.LoadFromPath(path);
                if (mapDef != null)
                {
                    Plugin.Log.LogInfo($"Loaded MapDef with display name: {mapDef.DisplayName}");
                    _mapDefs[path] = mapDef;
                }
            }

            ChangeAvailableMapDefs(FilteredMapDefs());
        }

        private IEnumerable<MapDef> FilteredMapDefs()
        {
            return _mapDefs.Values.Where(m =>
                {
                    if (_nameFilter.IsNullOrEmpty())
                    {
                        return true;
                    }

                    return m.MapInternalNames.Contains(_nameFilter);
                }).OrderBy(m => m.DisplayName);
        }

        public void FilterByInternalMapName(string internalMapName)
        {
            _nameFilter = internalMapName;
            ChangeAvailableMapDefs(FilteredMapDefs());
        }

        public void ClearFilter()
        {
            _nameFilter = null;
            ChangeAvailableMapDefs(FilteredMapDefs());
        }

        public void OnLoadMap(MapDef mapDef)
        {
            _dropdown.UpdateValue(_selectableMapDefs.IndexOf(mapDef));
        }
    }
}