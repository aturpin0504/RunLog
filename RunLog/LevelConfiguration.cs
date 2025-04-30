namespace RunLog
{
    public class LevelConfiguration
    {
        private readonly LoggerConfiguration _parent;

        internal LevelConfiguration(LoggerConfiguration parent)
        {
            _parent = parent;
        }

        public LoggerConfiguration Verbose()
        {
            return _parent.SetMinimumLevel(LogLevel.Verbose);
        }

        public LoggerConfiguration Debug()
        {
            return _parent.SetMinimumLevel(LogLevel.Debug);
        }

        public LoggerConfiguration Information()
        {
            return _parent.SetMinimumLevel(LogLevel.Information);
        }

        public LoggerConfiguration Warning()
        {
            return _parent.SetMinimumLevel(LogLevel.Warning);
        }

        public LoggerConfiguration Error()
        {
            return _parent.SetMinimumLevel(LogLevel.Error);
        }

        public LoggerConfiguration Fatal()
        {
            return _parent.SetMinimumLevel(LogLevel.Fatal);
        }
    }
}
