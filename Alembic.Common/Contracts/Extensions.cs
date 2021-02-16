using System;
using System.Collections.Generic;

namespace Alembic.Common.Contracts
{
    public static class Extensions
    {
        private const string ServiceNameKey = "com.docker.compose.service";
        private const string ContainerNumberKey = "com.docker.compose.container-number";
        private const string AutoHeal = "autoheal";
        private const string NotSet = "NotSet";

        public static string ExtractServiceLabelValue(this Dictionary<string, string> labels)
        {
            return ExtractLabelValue(labels, ServiceNameKey);
        }

        public static string ExtractContainerNumberLabelValue(this Dictionary<string, string> labels)
        {
            return ExtractLabelValue(labels, ContainerNumberKey);
        }

        public static bool ExtractAutoHealLabelValue(this Dictionary<string, string> labels)
        {
            var value = ExtractLabelValue(labels, AutoHeal);

            return bool.TryParse(value, out var autoHeal) && autoHeal;
        }

        public static string ExtractLabelValue(Dictionary<string, string> labels, string name)
        {
            _ = labels ?? throw new ArgumentNullException(nameof(labels));

            if (!labels.TryGetValue(name, out var value))
                return NotSet;

            return value;
        }
    }
}