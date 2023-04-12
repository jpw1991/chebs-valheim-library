using System;
using System.Collections.Generic;
using BepInEx.Configuration;

namespace ChebsValheimLibrary.Common
{
    public class MemoryConfigEntry<T, TT>
    {
        private readonly Func<T, TT> _processValueFunc;
        private T _lastKnownValue;
        private TT _processedValue;

        public TT Value => _processedValue;

        private readonly ConfigEntry<T> _configEntry;

        public MemoryConfigEntry(ConfigEntry<T> configEntry, Func<T, TT> processValueFunc)
        {
            _configEntry = configEntry;
            _processValueFunc = processValueFunc;
            _lastKnownValue = _configEntry.Value;
            _processedValue = _processValueFunc(_lastKnownValue);
            _configEntry.SettingChanged += OnConfigEntryValueChanged;
        }
        
        private void OnConfigEntryValueChanged(object sender, EventArgs args)
        {
            var value = _configEntry.Value;
            if (!EqualityComparer<T>.Default.Equals(value, _lastKnownValue))
            {
                _lastKnownValue = value;
                _processedValue = _processValueFunc(value);
            }
        }
    }
}