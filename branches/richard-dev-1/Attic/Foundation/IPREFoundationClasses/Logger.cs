using System;
using System.Collections.Generic;
using System.Text;
using IPREFoundationClasses.RobotGUI;

namespace IPREFoundationClasses
{
#if false
    public class Logger
    {
        Form1 gui;
        List<LogEntry> logList = new List<LogEntry>();

        public Logger(Form1 form)
        {
            gui = form;
        }

        public void Add(LogEntry entry)
        {
            logList.Add(entry);
            if (gui != null)
                refreshGUI();
        }

        public void refreshGUI()
        {
            gui.refreshLog(logList);
        }
    }

    public class LogEntry
    {
        LogLevel loglevel;
        String logtext;

        public LogLevel Level
        {
            get { return loglevel; }
            set { loglevel = value; }
        }

        public String LogText
        {
            get { return logtext; }
            set { logtext = value; }
        }

        public LogEntry(String str)
        {
            loglevel = LogLevel.Info;
            logtext = str;
        }

        public LogEntry(LogLevel level, String str)
        {
            loglevel = level;
            logtext = str;
        }
    }

    public enum LogLevel
    {
        Info,
        Debug,
        Warning,
        Error
    }
#endif
}
