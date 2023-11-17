﻿using DB;
using DB.JSON;
using ExternalData;
using Newtonsoft.Json;
using RaceLib;
using Sound;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Reflection.Metadata;
using System.Text;
using System.Threading;
using System.Web;
using Tools;

namespace Webb
{
    public class EventWebServer : IDisposable
    {
        private EventManager eventManager;
        private SoundManager soundManager;
        private IRaceControl raceControl;

        private Thread thread;
        public bool Running { get; private set; }

        private HttpListener listener;

        public string Url { get; private set; }

        public FileInfo CSSStyleSheet { get; private set; }

        public WebRaceControl WebRaceControl { get; private set; }

        private bool localOnly;

        private IWebbTable[] webbTables;

        public ToolColor[] ChannelColors { get; private set; }

        public EventWebServer(EventManager eventManager, SoundManager soundManager, IRaceControl raceControl, IEnumerable<IWebbTable> tables, IEnumerable<Tools.ToolColor> channelColors)
        {
            CSSStyleSheet = new FileInfo("httpfiles/style.css");
            this.eventManager = eventManager;
            this.soundManager = soundManager;
            this.raceControl = raceControl;
            WebRaceControl = new WebRaceControl(eventManager, soundManager, raceControl);
            Url = "http://localhost:8080/";

            if (!CSSStyleSheet.Exists)
            {
                FileStream fileStream = CSSStyleSheet.Create();
                fileStream.Dispose();
            }

            webbTables = tables.ToArray();

            ChannelColors = channelColors.ToArray();
        }

        public void Dispose() 
        {
            Stop();
        }


        public IEnumerable<string> GetPages()
        {
            yield return "Rounds";
            yield return "Event Status";

            foreach (IWebbTable table in webbTables)
            {
                yield return table.Name;
            }

            if (raceControl != null) 
            {
                yield return "RaceControl";
                yield return "Variable Viewer";
            }
        }

        public bool Start()
        {
            try
            {
                Running = true;

                if (thread != null)
                {
                    Stop();
                }
                
                thread = new Thread(Run);

                thread.Name = "Webb Thread";
                thread.Start();

                return true;
            }
            catch
            {
                return false;
            }


        }

        private void Run()
        {
            while (Running)
            {
                if (listener == null)
                {
                    CreateListener();
                }

                try
                {
                    HttpListenerContext context = listener.GetContext();
                    HandleRequest(context);

                }
                catch (Exception ex)
                {
                    Logger.HTTP.LogException(this, ex);
                    listener.Abort();
                    listener = null;
                }
            }
        }

        private void CreateListener()
        {
            try
            {
                // Try open all interfaces first
                listener = new HttpListener();
                listener.Prefixes.Add(Url.Replace("localhost", "+"));
                listener.Start();
                Logger.HTTP.Log(this, "Listening on " + listener.Prefixes.FirstOrDefault());
            }
            catch (Exception ex)
            {
                localOnly = true;
                Logger.HTTP.LogException(this, ex);

                // just open localhost
                listener = new HttpListener();
                listener.Prefixes.Add(Url);
                listener.Start();
                Logger.HTTP.Log(this, "Listening on " + listener.Prefixes.FirstOrDefault());
            }
        }

        private void HandleRequest(HttpListenerContext context)
        {
            HttpListenerResponse response = context.Response;

            if (context.Request.HttpMethod == "OPTIONS")
            {
                response.AddHeader("Access-Control-Allow-Headers", "Content-Type, Accept, X-Requested-With");
                response.AddHeader("Access-Control-Allow-Methods", "GET, POST");
                response.AddHeader("Access-Control-Max-Age", "1728000");
            }
            response.AppendHeader("Access-Control-Allow-Origin", "*");

            byte[] buffer = Response(context);
            // Get a response stream and write the response to it.
            response.ContentLength64 = buffer.Length;
            System.IO.Stream output = response.OutputStream;
            output.Write(buffer, 0, buffer.Length);
            // You must close the output stream.
            output.Close();
        }

        private byte[] Response(HttpListenerContext context)
        {
            string path = Uri.UnescapeDataString(context.Request.Url.AbsolutePath);
            string[] requestPath = path.Split('/').Where(s => !string.IsNullOrEmpty(s)).ToArray();

            NameValueCollection nameValueCollection;
            using (Stream receiveStream = context.Request.InputStream)
            {
                using (StreamReader readStream = new StreamReader(receiveStream, System.Text.Encoding.UTF8))
                {
                    string documentContents = readStream.ReadToEnd();
                    nameValueCollection = HttpUtility.ParseQueryString(documentContents);
                }
            }

            if (requestPath.Length == 0)
            {
                string content = "";
                foreach (string item in GetPages().OrderBy(r =>r))
                {
                    content += "<br><a class=\"menu\" href=\"/" + item + "/\">" + item + "</a> ";
                }

                if (localOnly)
                {
                    string url = listener.Prefixes.FirstOrDefault();
                    url = url.Replace("localhost", "+");
                    content += "<p>By default this webserver is only accessible from this machine. To access it over the network run in an Adminstrator command prompt:</p><p> netsh http add urlacl url = \"" + url + "\" user=everyone</p><p>Then restart the software</p>";
                }

                content += "<script>";
                content += "const content = document.getElementById(\"content\");";
                content += "var eventManager = new EventManager();";
                content += "var formatter = new Formatter(eventManager, content);";
                content += "</script>";

                content += "<a onclick=\"formatter.ShowRounds()\">Rounds</a>";
                content += "<a onclick=\"formatter.ShowLapRecords()\">Lap Records</a>";

                return GetFormattedHTML(context, content);
            }
            else
            {
                string action = requestPath[0];
                string[] parameters = requestPath.Skip(1).ToArray();

                string content = "";
                DirectoryInfo eventRoot = new DirectoryInfo("events/" + eventManager.Event.ID.ToString());
                switch (action)
                {
                    //case "VariableViewer":
                    //    VariableViewer vv = new VariableViewer(eventManager, soundManager);
                    //    content = vv.DumpObject(parameters, refresh, decimalPlaces);
                    //    break;

                    case "RaceControl":
                        if (nameValueCollection.Count > 0)
                        {
                            WebRaceControl.HandleInput(nameValueCollection);
                        }
                        content += WebRaceControl.GetHTML();
                        break;

                    case "httpfiles":
                    case "img":
                    case "themes":
                        string target1 =  string.Join('\\', requestPath);

                        if (File.Exists(target1))
                        {
                            return File.ReadAllBytes(target1);
                        }

                        break;

                    case "event":
                        string target = Path.Combine(eventRoot.FullName, string.Join('\\', requestPath.Skip(1)));

                        if (target == "")
                            target = eventRoot.FullName;

                        if (target.Contains("."))
                        {
                            if (File.Exists(target))
                            {
                                return File.ReadAllBytes(target);
                            }
                            return new byte[0];
                        }
                        else
                        {
                            DirectoryInfo di = new DirectoryInfo(Path.Combine(eventRoot.FullName, target));
                            if (eventRoot.Exists && di.Exists)
                            {
                                content += ListDirectory(eventRoot, di);
                            }
                        }
                        break;

                    case "races":
                        List<Guid> ids = new List<Guid>();

                        foreach (DirectoryInfo di in eventRoot.EnumerateDirectories())
                        {
                            if (Guid.TryParse(di.Name, out Guid id))
                            {
                                ids.Add(id);
                            }
                        }
                        return SerializeASCII(ids);

                    case "channelcolors":
                        List<ColoredChannel> colours = new List<ColoredChannel>();
                        foreach (RaceLib.Channel channel in eventManager.Channels)
                        {
                            string color = eventManager.GetChannelColor(channel).ToHex();
                            colours.Add(new ColoredChannel(channel, color));
                        }
                        return SerializeASCII(colours);

                    case "channels":
                        IEnumerable<DB.Channel> channels = RaceLib.Channel.AllChannels.Convert<DB.Channel>();
                        return SerializeASCII(channels);

                    case "Rounds":
                        content += WebbRounds.Rounds(eventManager);
                        break;

                    case "Event Status":
                        content += WebbRounds.EventStatus(eventManager, webbTables.FirstOrDefault());
                        break;

                    case "Lap Count":
                        IWebbTable webbTable2 = webbTables.FirstOrDefault(w => w.Name == action);
                        content += HTTPFormat.FormatTable(webbTable2, "");
                        break;

                    default:
                        IWebbTable webbTable = webbTables.FirstOrDefault(w => w.Name == action);
                        content += HTTPFormat.FormatTable(webbTable, "columns");
                        break;
                }

                return GetFormattedHTML(context, content);
            }
        }

        private byte[] SerializeASCII<T>(IEnumerable<T> ts)
        {
            JsonSerializerSettings settings = new JsonSerializerSettings()
            {
                Formatting = Formatting.Indented,
                DateFormatString = "yyyy/MM/dd H:mm:ss.FFF"
            };

            string json = JsonConvert.SerializeObject(ts, settings);
            return Encoding.ASCII.GetBytes(json);
        }
              
        private byte[] GetHTML(HttpListenerContext context, string content)
        {
            string refreshText = "";

            int decimalPlaces = 2;
            int refresh = 60;
            bool autoScroll = false;

            string query = context.Request.Url.Query;
            if (query.StartsWith("?"))
            {
                string[] queries = query.Split('?', '&');

                foreach (string q in queries)
                {
                    string[] split = q.Split('=');
                    if (split.Length == 2)
                    {
                        string key = split[0].ToLower();
                        string value = split[1];

                        switch (key)
                        {
                            case "refresh":
                                int.TryParse(value, out refresh);
                                break;
                            case "decimalplaces":
                                int.TryParse(value, out decimalPlaces);
                                break;
                            case "autoscroll":
                                bool.TryParse(value, out autoScroll);
                                break;
                        }
                    }
                }
            }
            

            if (refresh == 0)
            {
                refreshText = "";
            }
            else
            {
                refreshText = "<meta http-equiv=\"refresh\" content=\"" + refresh + "\" >";
            }

            string output = "<html><head>" + refreshText + "</head><link rel=\"stylesheet\" href=\"/httpfiles/style.css\">";

            if (autoScroll)
                output += "<script src=\"/httpfiles/scroll.js\"></script>";
            output += "<script src=\"/httpfiles/linq.js\"></script>";
            output += "<script src=\"/httpfiles/EventManager.js\"></script>";
            output += "<script src=\"/httpfiles/Formatter.js\"></script>";

            output += "<body id=\"body\">";

            output += content;
            

            output += "</body></html>";
            return Encoding.ASCII.GetBytes(output);
        }

        private byte[] GetFormattedHTML(HttpListenerContext context, string content)
        {
            string output = "<div class=\"top\">";
            output += "<img src=\"/img/logo.png\">";
            output += "<div class=\"time\">" + DateTime.Now.ToString("h:mm tt").ToLower() + "</div>";
            output += "</div>";


            output += "<div class=\"content\">";
            output += "<div id=\"content\"></div>";

            output += content;


            output += "</div>";

            return GetHTML(context, output);
        }

        private string ListDirectory(DirectoryInfo docRoot, DirectoryInfo target)
        {
            string content = "";

            string name = Path.GetRelativePath(docRoot.FullName, target.FullName);
            if (name == ".")
                name = "event";

            content += "<h1>" + name + "</h2>";
            content += "<ul>";

            foreach (DirectoryInfo subDir in target.GetDirectories())
            {
                content += "<li><a href=\"" + Path.GetRelativePath(docRoot.FullName, subDir.FullName) + " \">" + subDir.Name + "</a></li>";
            }

            foreach (FileInfo filename in target.GetFiles())
            {
                content += "<li><a href=\"" + Path.GetRelativePath(docRoot.FullName, filename.FullName) + " \">" + filename.Name + "</a></li>";
            }

            content += "</ul>";
            return content;
        }

        public bool Stop()
        {
            Running = false;
            listener?.Abort();
            thread?.Join();

            return true;
        }
    }
}
