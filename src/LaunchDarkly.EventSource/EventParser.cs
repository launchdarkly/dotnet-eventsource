using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace LaunchDarkly.EventSource
{
    public class EventParser
    {

        //public void ParseLine(string line)
        //{
        //    if (string.IsNullOrEmpty(line.Trim()))
        //    {
        //        dispatchEvent();
        //    }
        //    else if (line.StartsWith(":", StringComparison.OrdinalIgnoreCase))
        //    {
        //        ProcessComment(line.Substring(1).Trim());
        //    }
        //    else if (line.IndexOf(":", StringComparison.Ordinal) != -1)
        //    {
        //        var colonIndex = line.IndexOf(":", StringComparison.Ordinal);

        //        string field = line.Substring(0, colonIndex);
        //        string value = line.Substring(colonIndex + 1).TrimStart(' ');

        //        ProcessField(field, value);
        //    }
        //    else
        //    {
        //        ProcessField(line.Trim(), string.Empty);
        //    }
        //}

        //private void ProcessComment(String comment)
        //{
        //    try
        //    {
        //        handler.onComment(comment);
        //    }
        //    catch (Exception e)
        //    {
        //        handler.onError(e);
        //    }
        //}

        //private void ProcessField(string field, string value)
        //{
        //    if (Constants.DataField.Equals(field, StringComparison.OrdinalIgnoreCase))
        //    {
        //        data.append(value).append("\n");
        //    }
        //    else if (Constants.IdField.Equals(field, StringComparison.OrdinalIgnoreCase))
        //    {
        //        lastEventId = value;
        //    }
        //    else if (Constants.EventField.Equals(field, StringComparison.OrdinalIgnoreCase))
        //    {
        //        eventName = value;
        //    }
        //    else if (Constants.RetryField.Equals(field, StringComparison.OrdinalIgnoreCase) && IsNumeric(value))
        //    {
        //        connectionHandler.setReconnectionTimeMs(Long.parseLong(value));
        //    }
        //}

        //private bool IsNumeric(string input)
        //{
        //    return Regex.IsMatch(input, @"^[\d]+$");
        //}

        //private void dispatchEvent()
        //{
        //    if (data.length() == 0)
        //    {
        //        return;
        //    }
        //    String dataString = data.toString();
        //    if (dataString.endsWith("\n"))
        //    {
        //        dataString = dataString.substring(0, dataString.length() - 1);
        //    }
        //    MessageEvent message = new MessageEvent(dataString, lastEventId, origin);
        //    connectionHandler.setLastEventId(lastEventId);
        //    try
        //    {
        //        handler.onMessage(eventName, message);
        //    }
        //    catch (Exception e)
        //    {
        //        handler.onError(e);
        //    }
        //    data = new StringBuffer();
        //    eventName = DEFAULT_EVENT;
        //}
    }
}
