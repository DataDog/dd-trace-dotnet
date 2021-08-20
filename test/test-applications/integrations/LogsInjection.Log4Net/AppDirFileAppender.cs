using System.IO;
using log4net.Appender;

namespace LogsInjection.Log4Net
{
    /// <summary>
    /// See the following StackOverflow link for overwriting built-in appenders: https://stackoverflow.com/questions/1922430/how-do-you-make-log4net-output-to-current-working-directory
    /// </summary>
    public class AppDirFileAppender : FileAppender
    {
        public override string File
        {
            set
            {
                var directory = Directory.GetParent(typeof(AppDirFileAppender).Assembly.Location).FullName;
                base.File = Path.Combine(directory, value);
            }
        }
    }
}
