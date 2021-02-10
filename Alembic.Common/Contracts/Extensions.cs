using System;

namespace Alembic.Common.Contracts
{
    public static class Extensions
    {
        private const string ServiceNameKey = "com.docker.compose.service";
        private const string ContainerNumberKey = "com.docker.compose.container-number";
        private const string AutoHeal = "autoheal";
        private const string NotSet = "NotSet";

        public static string ExtractServiceLabelValue(this Container container)
        {
            return ExtractLabelValue(container, ServiceNameKey);
        }

        public static string ExtractContainerNumberLabelValue(this Container container)
        {
            return ExtractLabelValue(container, ContainerNumberKey);
        }

        public static bool ExtractAutoHealLabelValue(this Container container)
        {
            var value = ExtractLabelValue(container, AutoHeal);

            return bool.TryParse(value, out var autoHeal) && autoHeal;
        }

        private static string ExtractLabelValue(Container container, string name)
        {
            _ = container ?? throw new ArgumentNullException(nameof(container));

            if (container.Config == null)
                return NotSet;

            if (!container.Config.Labels.TryGetValue(name, out var value))
                return NotSet;

            return value;
        }
    }
}