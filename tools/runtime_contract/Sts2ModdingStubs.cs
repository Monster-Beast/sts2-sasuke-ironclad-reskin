namespace MegaCrit.Sts2.Core.Modding
{
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class ModInitializerAttribute : Attribute
    {
        public ModInitializerAttribute(string initializerName)
        {
            _ = initializerName;
        }
    }
}

namespace MegaCrit.Sts2.Core.Logging
{
    public enum LogType
    {
        Generic = 0,
    }

    public sealed class Logger
    {
        public Logger(string modId, LogType logType)
        {
            _ = modId;
            _ = logType;
        }
    }
}
