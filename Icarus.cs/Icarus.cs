using System;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using WindowsGSM.Functions;
using WindowsGSM.GameServer.Engine;
using WindowsGSM.GameServer.Query;
using System.IO;
using System.Net;
using System.Linq;
using System.Windows;
using System.Runtime.InteropServices;

namespace WindowsGSM.Plugins
{
    public class Icarus : SteamCMDAgent
    {
        [DllImport("kernel32.dll", EntryPoint = "WritePrivateProfileString")]
        public static extern bool WritePrivateProfileString(string strSection,
                                                            string strKeyName,
                                                            string strValue,
                                                            string strFilePath);
        // - Plugin Details
        public Plugin Plugin = new Plugin
        {
            name = "WindowsGMS.Icarus",
            author = "BizakDaTroll",
            description = "WindowsGMS plugin for Icarus Dedicated Server",
            version = "0.5",
            url = "https://github.com/BizakDaTroll/WindowsGSM",
            color = "#ffffff"
        };


        // - Standard Constructor and properties
        public Icarus(ServerConfig serverData) : base(serverData) => base.serverData = _serverData = serverData;
        private readonly ServerConfig _serverData;


        // - Settings properties for SteamCMD installer
        public override bool loginAnonymous => true;
        public override string AppId => "2089300";




        // - Game server Fixed variables
        public override string StartPath => @"Icarus\Binaries\win64\IcarusServer-Win64-Shipping.exe";
        public string FullName = "Icarus Dedicated Server";
        public bool AllowsEmbedConsole = true;
        public int PortIncrements = 10;
        public object QueryMethod = null;


        // - Game server default values
        public string Port = "17777";
        public string QueryPort = "27015";
        public string Defaultmap = "";
        public string Maxplayers = "8";
        public string Additional = "";


        // - Create a default cfg for the game server after installation
        public async void CreateServerCFG()
        {
            //Download server.properties
            string configPath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, @"Icarus\Saved\Config\WindowsServer\ServerSettings.ini");
            string IcarusCFGURL = "https://raw.githubusercontent.com/RocketWerkz/IcarusDedicatedServer/main/ServerSettings.ini";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(configPath));
                using (WebClient IcarusCFG = new WebClient())
                {
                    await IcarusCFG.DownloadFileTaskAsync(IcarusCFGURL, configPath);
                }
                string section = "/Script/Icarus.DedicatedServerSettings";
                WritePrivateProfileString(section, "MaxPlayers", Maxplayers, configPath);
            }
            catch (Exception e)
            {
                Error = "CFG Error" + e.Message;
                return;
            }

        }

        // - Start server function, return its Process to WindowsGSM
        public async Task<Process> Start()
        {
            string shipExePath = Functions.ServerPath.GetServersServerFiles(_serverData.ServerID, StartPath);
            if (!File.Exists(shipExePath))
            {
                Error = $"{Path.GetFileName(shipExePath)} not found ({shipExePath})";
                return null;
            }

            // Prepare start parameter

            string param = $"{_serverData.ServerParam}";
            param += "-NOSTEAM -log";
            param += string.IsNullOrWhiteSpace(_serverData.ServerPort) ? string.Empty : $" -PORT={_serverData.ServerPort}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerQueryPort) ? string.Empty : $" -QueryPort={_serverData.ServerQueryPort}";
            param += string.IsNullOrWhiteSpace(_serverData.ServerName) ? string.Empty : $" -SteamServerName={_serverData.ServerName}";
            // param += string.IsNullOrWhiteSpace(_serverData.ServerMaxPlayer) ? string.Empty : $" -MaxPlayers={_serverData.ServerMaxPlayer}";



            // Prepare Process
            var p = new Process
            {
                StartInfo =
                {
                    WorkingDirectory = ServerPath.GetServersServerFiles(_serverData.ServerID),
                    FileName = shipExePath,
                    Arguments = param,
                    WindowStyle = ProcessWindowStyle.Minimized,
                    UseShellExecute = false
                },
                EnableRaisingEvents = true
            };

            // Set up Redirect Input and Output to WindowsGSM Console if EmbedConsole is on
            if (AllowsEmbedConsole)
            {
                p.StartInfo.CreateNoWindow = true;
                p.StartInfo.RedirectStandardInput = true;
                p.StartInfo.RedirectStandardOutput = true;
                p.StartInfo.RedirectStandardError = true;
                var serverConsole = new ServerConsole(_serverData.ServerID);
                p.OutputDataReceived += serverConsole.AddOutput;
                p.ErrorDataReceived += serverConsole.AddOutput;

                // Start Process
                try
                {
                    p.Start();
                    if (AllowsEmbedConsole)
                    {
                        p.BeginOutputReadLine();
                        p.BeginErrorReadLine();
                    }
                    return p;
                }
                catch (Exception e)
                {
                    Error = e.Message;
                    return null; // return null if fail to start
                }

            }

            // Start Process
            try
            {
                p.Start();
                return p;
            }
            catch (Exception e)
            {
                Error = e.Message;
                return null; // return null if fail to start
            }
        }
        // - Stop server function
        public async Task Stop(Process p)
        {
            await Task.Run(() =>
            {
                Functions.ServerConsole.SetMainWindow(p.MainWindowHandle);
                Functions.ServerConsole.SendWaitToMainWindow("^c");
            });
            await Task.Delay(20000);
        }
    }
}