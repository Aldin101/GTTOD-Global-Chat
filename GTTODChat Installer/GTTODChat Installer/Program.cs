using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.IO.Compression;
using System.Reflection;
using Microsoft.Win32;
using Gameloop.Vdf;
using Gameloop.Vdf.Linq;

namespace GTTODChat_Installer
{
    class Start
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            new Menu();
        }
    }

    public class Menu : Form
    {
        public Menu()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("chat.zip"));
            int randomNumber = new Random().Next(1000, 9999);
            while (Directory.Exists(Path.Combine(Path.GetTempPath() + "GTTODChatInstaller" + randomNumber)))
            {
                randomNumber = new Random().Next(1000, 9999);
            }
            string tempPath = Path.Combine(Path.GetTempPath(), "GTTODChatInstaller" + randomNumber);
            Directory.CreateDirectory(tempPath);

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var fileStream = File.Create(Path.Combine(tempPath, "chat.zip")))
                {
                    stream.CopyTo(fileStream);
                }
            }

            ZipFile.ExtractToDirectory(Path.Combine(tempPath, "chat.zip"), tempPath);

            File.Delete(Path.Combine(tempPath, "chat.zip"));

            resourceName = assembly.GetManifestResourceNames().Single(str => str.EndsWith("Gameloop.Vdf.dll"));
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var fileStream = File.Create("Gameloop.Vdf.dll"))
                {
                    stream.CopyTo(fileStream);
                }
            }


            Text = "GTTOD Global Chat Installer";
            Size = new System.Drawing.Size(240, 200);
            StartPosition = FormStartPosition.CenterScreen;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;

            Label title = new Label();

            title.Text = "GTTOD Global Chat";
            title.Location = new System.Drawing.Point(5, 10);
            title.Size = new System.Drawing.Size(210, 30);
            title.Font = new System.Drawing.Font("Arial", 14, System.Drawing.FontStyle.Bold);
            title.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            Controls.Add(title);

            TextBox path = new TextBox();
            path.Text = "placeholder";
            path.Location = new System.Drawing.Point(10, 50);
            path.Size = new System.Drawing.Size(200, 30);
            Controls.Add(path);

            Button install = new Button();
            install.Text = "Install";
            install.Location = new System.Drawing.Point(10, 100);
            install.Size = new System.Drawing.Size(100, 30);
            install.Click += (sender, e) => { Install(path.Text, tempPath); };
            Controls.Add(install);

            Button uninstall = new Button();
            uninstall.Text = "Uninstall";
            uninstall.Location = new System.Drawing.Point(110, 100);
            uninstall.Size = new System.Drawing.Size(100, 30);
            uninstall.Click += (sender, e) => { Uninstall(path.Text); };
            Controls.Add(uninstall);

            try
            {
                path.Text = searchForGameFolder();
            }
            catch (Exception e)
            {
                path.Text = e.Message;
            }

            ShowDialog();

            Directory.Delete(tempPath, true);

        }

        public string searchForGameFolder()
        {
            string steamPath = getSteamPath();
            if (steamPath == null) throw new Exception("Steam not found, please enter path manually");

            VProperty libraryFolders = VdfConvert.Deserialize(File.ReadAllText(steamPath + @"\steamapps\libraryfolders.vdf"));

            foreach (var item in libraryFolders.Value.Children<VProperty>())
            {
                if (int.TryParse(item.Key, out int _))
                {
                    string libraryPath = item.Value["path"].Value<string>();
                    string gamePath = Path.Combine(libraryPath, "steamapps", "common", "Get To The Orange Door");

                    if (Directory.Exists(gamePath))
                    {
                        return gamePath;
                    }
                }
            }
            throw new Exception("Game not found, please enter path manually");
        }

        public string getSteamPath()
        {
            string keyName = @"SOFTWARE\Valve\Steam";
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyName))
            {
                if (key != null)
                {
                    object steamPath = key.GetValue("SteamPath");
                    if (steamPath != null)
                    {
                        return steamPath.ToString();
                    }
                }
            }
            return null;
        }

        public void Install(string path, string tempPath)
        {

            CopyAll(new DirectoryInfo(tempPath), new DirectoryInfo(path));

            MessageBox.Show("GTTOD Global Chat installed!", "GTTOD Global Chat Installer", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        public void CopyAll(DirectoryInfo source, DirectoryInfo target)
        {
            if (source.FullName.ToLower() == target.FullName.ToLower())
            {
                return;
            }

            // Check if the target directory exists, if not, create it.
            if (Directory.Exists(target.FullName) == false)
            {
                Directory.CreateDirectory(target.FullName);
            }

            // Copy each file into it's new directory.
            foreach (FileInfo fi in source.GetFiles())
            {
                fi.CopyTo(Path.Combine(target.ToString(), fi.Name), true);
            }

            // Copy each subdirectory using recursion.
            foreach (DirectoryInfo diSourceSubDir in source.GetDirectories())
            {
                DirectoryInfo nextTargetSubDir =
                    target.CreateSubdirectory(diSourceSubDir.Name);
                CopyAll(diSourceSubDir, nextTargetSubDir);
            }
        }


        public void Uninstall(string path)
        {
            if (!File.Exists(Path.Combine(path, "BepInEx", "plugins", "GTTODGlobalChat.dll")))
            {
                MessageBox.Show("GTTOD Global Chat not installed", "GTTOD Global Chat Installer", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            File.Delete(Path.Combine(path, "BepInEx", "plugins", "GTTODGlobalChat.dll"));
            DialogResult result = MessageBox.Show("Would you like to uninstall BepInEx as well?", "GTTOD Global Chat Installer", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (result == DialogResult.Yes)
            {
                Directory.Delete(Path.Combine(path, "BepInEx"), true);
                Directory.Delete(Path.Combine(path, "unstrip"), true);
                File.Delete(Path.Combine(path, "winhttp.dll"));
                File.Delete(Path.Combine(path, "doorstop_config.ini"));
            }
        }
    }
}
