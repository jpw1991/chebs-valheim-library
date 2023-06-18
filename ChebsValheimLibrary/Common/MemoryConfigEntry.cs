using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using Jotunn;

namespace ChebsValheimLibrary.Common
{
    public class MemoryConfigEntry<T, TT>
    {
        private readonly Func<T, TT> _processValueFunc;
        private T _lastKnownValue;
        private TT _processedValue;

        public TT Value => _processedValue;

        public ConfigEntry<T> ConfigEntry => _configEntry;

        private readonly ConfigEntry<T> _configEntry;

        public MemoryConfigEntry(ConfigEntry<T> configEntry, Func<T, TT> processValueFunc)
        {
            _configEntry = configEntry;
            _processValueFunc = processValueFunc;
            _lastKnownValue = _configEntry.Value;
            _processedValue = _processValueFunc(_lastKnownValue);
            if (configEntry == null)
            {
                Logger.LogError("MemoryConfigEntry: configEntry is null!");
                return;
            }
            _configEntry.SettingChanged += OnConfigEntryValueChanged;
        }
        
        private void OnConfigEntryValueChanged(object sender, EventArgs args)
        {
            var value = _configEntry.Value;
            if (value == null)
            {
                Logger.LogError("MemoryConfigEntry.OnConfigEntryValueChanged: value is null!");
                return;
            }
            if (!EqualityComparer<T>.Default.Equals(value, _lastKnownValue))
            {
                Logger.LogInfo($"MemoryConfigEntry.OnConfigEntryValueChanged: value is new! {value}");
                _lastKnownValue = value;
                _processedValue = _processValueFunc(value);
            }
        }
    }
}