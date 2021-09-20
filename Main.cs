using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using SharpCompress.Archives.Rar;
using SharpCompress.Common;
using SharpCompress.Archives;
using SharpCompress.Archives.Zip;
using System.Diagnostics;
using SharpCompress.Archives.SevenZip;
using System.Text.RegularExpressions;
using Microsoft.WindowsAPICodePack.Dialogs;
using CodeWalker.GameFiles;
using CodeWalker.Utils;
using ytd2large.HashHelpers;

namespace ytd2large
{

    public partial class Main : Form
    {

        static Dictionary<string, string[]> extensions = new Dictionary<string, string[]>()
        {
            { "meta",  new string[]{ ".meta", "clip_sets.xml" } },
            { "stream", new string[]{".ytd", ".yft", ".ydr" } }
        };

        static Dictionary<string, string> modelNames = new Dictionary<string, string>();

        public Main()
        {
            InitializeComponent();
            if (!Directory.Exists("./logs"))
            {
                Directory.CreateDirectory("logs");
            }
            if (!File.Exists(@"./logs/latest.log"))
            {
                FileStream fs = File.Create(@"./logs/latest.log");
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {

            LogAppend("ytd2large");
            LogAppend("---------------");
            LogAppend("Developed by Avenzey#6184 (thanks to https://github.com/vscorpio for developing parts of the code!)");
            LogAppend("GitHub repository: https://github.com/Avenze/ytd2large-repository");
            LogAppend("Discord support: https://discord.gg/C4e4q6g");
            LogAppend("---------------");

            if (!Directory.Exists("./library/nconvert"))
            {
                // add warning if the user hasn't installed NConvert properly
                WarningAppend("[NConvert] It seems like you haven't installed NConvert, please follow");
                WarningAppend("[NConvert] the installation instructions on the GitHub Repository or the forum thread.");
                WarningAppend("[NConvert] ytd2large WILL error and not work as intented, do not continue.");
            }
            if (!Directory.Exists("./library/gtautil"))
            {
                // add warning if the user hasn't installed gtautil properly
                WarningAppend("[GTAUtil] It seems like the GTAUtil executable is missing, please follow");
                WarningAppend("[GTAUtil] the installation instructions on the GitHub Repository or the forum thread.");
                WarningAppend("[GTAUtil] ytd2large WILL error and not work as intented, do not continue.");
            }

            // input and output directory creation stuff
            if (!Directory.Exists("./input"))
            {
                LogAppend("Creating ./input directory as it was missing.");
                Directory.CreateDirectory("input");
            }
            if (!Directory.Exists("./output"))
            {
                LogAppend("Creating ./output directory as it was missing.");
                Directory.CreateDirectory("output");
            }
        }

        // Helper Functions

        public void LogAppend(string text)
        {
            log.AppendText(text + Environment.NewLine);
            StatusHandler(text);
            LogFile("[INFO] " + text);
        }

        public void WarningAppend(string text)
        {
            log.AppendText(text + Environment.NewLine);
            StatusHandler(text);
            LogFile("[WARNING] " + text);
        }

        public void ErrorAppend(string text)
        {
            log.AppendText("[Error] An error occoured during execution, stacktrace has been logged to /logs/latest.log, please submit to GitHub Issues page.");
            LogFile("[ERROR] " + text);
        }

        public void LogFile(string text)
        {
            try
            {
                string currentDate = DateTime.Now.ToString(@"MM\/dd\/yyyy\ hh\:mm\:ss");
                using (TextWriter tw = new StreamWriter(@"./logs/latest.log", append: true))
                {
                    tw.WriteLine("[" + currentDate + "] " + text + Environment.NewLine);
                    tw.Close();
                }
            }
            catch (Exception ex)
            {
                ErrorAppend("[Worker] Failed to write log to file. Stacktrace: " + ex);
            }
        }

        private void StatusHandler(string status)
        {
            tsStatus.Text = "Status: " + status;
        }

        private void QueueHandler(int current, int total)
        {
            tsQueue.Text = "Queue: " + current + "/" + total;
        }

        private void ShellCmd(string cmd)
        {
            string strCmdText = "/K " + cmd;
            System.Diagnostics.Process.Start("CMD.exe", strCmdText);
        }

        private void HideShellCmd(string cmd)
        {
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "cmd.exe";
            startInfo.Arguments = "/C " + cmd;
            process.StartInfo = startInfo;
            process.Start();
        }

        // Loop

        private async void StartLoop()
        {
            while (true)
            {
                await Task.Delay(1000);
                string[] ytdFiles = Directory.GetFiles("input", "*.ytd", SearchOption.AllDirectories);

                ytdList.Items.Clear();

                foreach (var ytdFile in ytdFiles)
                {
                    ytdList.Items.Add($"{ytdFile}");
                }
            }
        }

        // Unpacking Functions

        private void ExtractFilesInRPF(RpfFile rpf, string directoryOffset)
        {
            using (BinaryReader br = new BinaryReader(File.OpenRead(rpf.GetPhysicalFilePath())))
            {
                foreach (RpfEntry entry in rpf.AllEntries)
                {
                    if (!entry.NameLower.EndsWith(".rpf")) //don't try to extract rpf's, they will be done separately..
                    {
                        if (entry is RpfBinaryFileEntry)
                        {
                            RpfBinaryFileEntry binentry = entry as RpfBinaryFileEntry;
                            byte[] data = rpf.ExtractFileBinary(binentry, br);
                            if (data == null)
                            {
                                if (binentry.FileSize == 0)
                                {
                                    LogAppend("[CodeWalker] Invalid binary filesize!");
                                }
                                else
                                {
                                    LogAppend("[CodeWalker] Binary data is null");
                                }
                            }
                            else if (data.Length == 0)
                            {
                                LogAppend("[CodeWalker] Decompressed output " + entry.Path + " was empty!");
                            }
                            else
                            {
                                File.WriteAllBytes(directoryOffset + entry.NameLower, data);
                            }
                        }
                        else if (entry is RpfResourceFileEntry)
                        {
                            RpfResourceFileEntry resentry = entry as RpfResourceFileEntry;
                            byte[] data = rpf.ExtractFileResource(resentry, br);
                            data = ResourceBuilder.Compress(data); //not completely ideal to recompress it...
                            data = ResourceBuilder.AddResourceHeader(resentry, data);
                            if (data == null)
                            {
                                if (resentry.FileSize == 0)
                                {
                                    LogAppend("[CodeWalker] Resource (" + entry.Path + ") filesize was empty!");
                                }
                            }
                            else if (data.Length == 0)
                            {
                                LogAppend("[CodeWalker] Decompressed output (" + entry.Path + ") was empty!");
                            }
                            else
                            {
                                foreach (KeyValuePair<string, string[]> extensionMap in extensions)
                                {
                                    foreach (string extension in extensionMap.Value)
                                    {
                                        if (entry.NameLower.EndsWith(extension))
                                        {

                                            if (extension.Equals(".ytd"))
                                            {
                                                if (true == true) // Too lazy to retab the code.
                                                {
                                                    RpfFileEntry rpfentry = entry as RpfFileEntry;

                                                    byte[] ytddata = rpfentry.File.ExtractFile(rpfentry);

                                                    YtdFile ytd = new YtdFile();
                                                    ytd.Load(ytddata, rpfentry);

                                                    Dictionary<uint, Texture> Dicts = new Dictionary<uint, Texture>();

                                                    bool somethingResized = false;
                                                    foreach (KeyValuePair<uint, Texture> texture in ytd.TextureDict.Dict)
                                                    {
                                                        if (texture.Value.Width > 512) // Only resize if it is greater than 1440p
                                                        {
                                                            byte[] dds = DDSIO.GetDDSFile(texture.Value);
                                                            File.WriteAllBytes("./NConvert/" + texture.Value.Name + ".dds", dds);

                                                            Process p = new Process();
                                                            p.StartInfo.FileName = @"./library/nconvert/nconvert.exe";
                                                            p.StartInfo.Arguments = $"-out dds -resize 50% 50% -overwrite ./NConvert/{texture.Value.Name}.dds";
                                                            p.StartInfo.UseShellExecute = false;
                                                            p.StartInfo.RedirectStandardOutput = true;
                                                            p.Start();

                                                            p.WaitForExit();

                                                            LogAppend("[NConvert] Sucessfully resized texture (" + texture.Value.Name + ") to 50%!");
                                                            File.Move("./NConvert/" + texture.Value.Name + ".dds", directoryOffset + texture.Value.Name + ".dds");

                                                            byte[] resizedData = File.ReadAllBytes(directoryOffset + texture.Value.Name + ".dds");
                                                            Texture resizedTex = DDSIO.GetTexture(resizedData);
                                                            resizedTex.Name = texture.Value.Name;
                                                            Dicts.Add(texture.Key, resizedTex);

                                                            File.Delete(directoryOffset + texture.Value.Name + ".dds");
                                                            somethingResized = true;
                                                        }
                                                        else
                                                        {
                                                            Dicts.Add(texture.Key, texture.Value);
                                                        }
                                                    }

                                                    if (!somethingResized)
                                                    {
                                                        LogAppend("[CodeWalker] No textures were resized, skipping .ytd recreation.");
                                                        break;
                                                    }

                                                    TextureDictionary dictionary = new TextureDictionary();
                                                    dictionary.Textures = new ResourcePointerList64<Texture>();
                                                    dictionary.TextureNameHashes = new ResourceSimpleList64_uint();
                                                    dictionary.Textures.data_items = Dicts.Values.ToArray();
                                                    dictionary.TextureNameHashes.data_items = Dicts.Keys.ToArray();

                                                    dictionary.BuildDict();
                                                    ytd.TextureDict = dictionary;

                                                    byte[] resizedYtdData = ytd.Save();
                                                    File.WriteAllBytes(directoryOffset + entry.NameLower, resizedYtdData);

                                                    LogAppend("[CodeWalker] Resized texture dictionary (ytd) " + entry.NameLower + ".");
                                                    break;
                                                }
                                            }

                                            File.WriteAllBytes(directoryOffset + entry.NameLower, data);
                                            break;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else
                    {
                        RpfBinaryFileEntry binaryentry = entry as RpfBinaryFileEntry;
                        byte[] data = rpf.ExtractFileBinary(binaryentry, br);
                        File.WriteAllBytes(directoryOffset + entry.NameLower, data);

                        RpfFile subRPF = new RpfFile(directoryOffset + entry.NameLower, directoryOffset + entry.NameLower);

                        if (subRPF.ScanStructure(null, null))
                        {
                            ExtractFilesInRPF(subRPF, directoryOffset);
                        }
                        File.Delete(directoryOffset + entry.NameLower);
                    }
                }
            }

        }

        // Conversion Functions
        public async Task startConversion(string resname, string link, bool combine, bool local)
        {

            // Archive .ytd files into .rpf archive for easier structure and handling.
            LogAppend("[GTAUtil] Archiving .ytd files into .rpf archive...");
            try
            {
                HideShellCmd(@"library\gtautil\GTAUtil.exe createarchive --input input\ --output \ --name dlc");
            }
            catch (Exception ex)
            {
                ErrorAppend("[GTAUtil] Failed to archive .ytd files into .rpf archive. Stacktrace: " + ex);
            }

            // Use built in functions to unarchive the .rpf archive and use the NConvert feature persson#4395 built into it. Hacky method but whatever, PR if you improve it.
            LogAppend("[Worker] Unarchiving .rpf and resizing images using NConvert.");
            try
            {
                RpfFile rpf = new RpfFile(@"\dlc.rpf", @"\dlc.rpf");
                LogAppend("[CodeWalker] Unpacking dlc.rpf...");

                if (rpf.ScanStructure(null, null))
                {
                    ExtractFilesInRPF(rpf, @".\cache\rpfunpack\"); 
                }
            }
            catch (Exception ex)
            {
                ErrorAppend("[CodeWalker] Failed to find the dlc.rpf file, or the structure scanning failed. Stacktrace: " + ex);
            }

            // Moving resized .ytd files from /cache/rpfunpack to /output.
            string[] ytdFiles = Directory.GetFiles("cache", "*.ytd", SearchOption.AllDirectories);

            foreach (var ytdFile in ytdFiles)
            {
                LogAppend("[CodeWalker] Moving " + ytdFile + " to /output.");
            }

            // Clean up like you just murdured someone.
            Directory.Delete("cache", true); 
            Directory.Delete("input", true);
            File.Delete("dlc.rpf"); // Will cause issues next time the program is used unless we delete it.

        }

        // Events

        private void checkBox1_CheckedChanged_1(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();
        }

        private async void button2_Click_1(object sender, EventArgs e)
        {
           
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            
        }

        //
        // gta folder stuffs
        //

        private bool checkGtaFolder()
        {
            if (folderPath.Text.Contains(@"\Grand Theft Auto V") || folderPath.Text.Contains(@"\GTAV") && !folderPath.Text.Contains(@"GTA5.exe"))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        async Task makeGtaUtilFolder(string gtaUtilTempFolder, string gtafolder)
        {
            string xmlTemplate = $@"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <configuration>
                                        <configSections>
                                            <sectionGroup name=""userSettings"" type=""System.Configuration.UserSettingsGroup, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" >
                                                <section name=""GTAUtil.Settings"" type=""System.Configuration.ClientSettingsSection, System, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"" allowExeDefinition=""MachineToLocalUser"" requirePermission=""false"" />
                                            </sectionGroup>
                                        </configSections>
                                        <userSettings>
                                            <GTAUtil.Settings>
                                                <setting name=""GTAFolder"" serializeAs=""String"">
                                                    <value>{gtafolder}</value>
                                                </setting>
                                            </GTAUtil.Settings>
                                        </userSettings>
                                    </configuration>";


            try
            {
                Directory.CreateDirectory(ytd2large.HashHelpers.Helpers.GetLocalAppDataUserConfigPathNoUserConfigFolder(Path.Combine(Directory.GetCurrentDirectory(), @"library\gtautil\GTAUtil.exe")));
                await Task.Delay(1000);
                File.WriteAllText(gtaUtilTempFolder, xmlTemplate, Encoding.Default);
            }
            catch (Exception ex)
            {
                ErrorAppend("[Worker] Failed to create GTAUtil folder. Stacktrace: " + ex);
            }
        }

        private async void btnAddQueue_Click(object sender, EventArgs e)
        {
            CommonOpenFileDialog dialog = new CommonOpenFileDialog();
            dialog.InitialDirectory = "C:\\Users";
            dialog.IsFolderPicker = true;
            if (dialog.ShowDialog() == CommonFileDialogResult.Ok)
            {
                folderPath.Text = dialog.FileName;
                if (checkGtaFolder())
                {
                    await makeGtaUtilFolder(ytd2large.HashHelpers.Helpers.GetExeLocalAppDataUserConfigPath(Path.Combine(Directory.GetCurrentDirectory(), @"library\gtautil\GTAUtil.exe")), dialog.FileName);
                    btnStart.Enabled = true;
                    folderStatus.Text = "OK";
                    folderStatus.ForeColor = Color.Green;
                }
                else
                {
                    btnStart.Enabled = false;
                    folderStatus.Text = "NOT SET";
                    folderStatus.ForeColor = Color.Red;
                }
            }
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {

        }

        private void reslua_TextChanged(object sender, EventArgs e)
        {

        }

        private void checkBox1_CheckedChanged_2(object sender, EventArgs e)
        {

        }

        private void groupBox1_Enter(object sender, EventArgs e)
        {

        }

        private void queueList_SelectedIndexChanged(object sender, EventArgs e)
        {

        }
    }
}

