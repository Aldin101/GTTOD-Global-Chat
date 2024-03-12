using System;
using System.Net.Sockets;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using Microsoft.Win32;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using BepInEx;

namespace Client
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private bool connectionAttempted = false;
        private bool connectionFailed = false;
        private TcpClient client;
        private NetworkStream stream;

        private Material defaultMateral;

        private GameObject hud = null;
        private GameObject chatbox = null;
        private GameObject input = null;
        private GameObject messages = null;

        private bool textBoxFocused = false;

        private AssetBundle bundle;
        private Font font;
        private Sprite sprite;

        private string username;

        private void Awake()
        {
            Logger.LogInfo($"{PluginInfo.PLUGIN_NAME} V{PluginInfo.PLUGIN_VERSION} has loaded");

            username = getSteamUsername();
        }

        private void OnDestroy()
        {
            if (client != null)
            {
                client.Close();
            }
            GameObject.Destroy(chatbox);
            bundle.Unload(true);
        }

        private void Update()
        {
            if (GameManager.GM == null) return;
            if (GameManager.GM.gameObject.GetComponent<GTTOD_HUD>().BigTextGroup.transform.parent == null) return;
            hud = GameManager.GM.gameObject.GetComponent<GTTOD_HUD>().BigTextGroup.transform.parent.gameObject;

            if (chatbox == null)
            {
                chatbox = createChatBox();

                chatbox.transform.SetParent(hud.transform);
                chatbox.transform.localPosition = new Vector3(600, 325, 0);
                chatbox.transform.localScale = new Vector3(1, 1, 1);
                chatbox.transform.localRotation = Quaternion.Euler(356.5f, 3.5f, 0);

                GameObject panel = chatbox.transform.GetChild(0).gameObject;
                panel.transform.localPosition = new Vector3(0, 0, 0);
                panel.transform.localScale = new Vector3(1, 1, 1);

                GameObject textObject = panel.transform.GetChild(0).gameObject;
                textObject.transform.localPosition = new Vector3(-135, -53, 0);
                textObject.transform.localScale = new Vector3(.15f, .15f, 1);

                GameObject inputFieldObject = panel.transform.GetChild(1).gameObject;
                inputFieldObject.transform.localPosition = new Vector3(-135, -75, 0);
                inputFieldObject.transform.localScale = new Vector3(.15f, .15f, 1);
            }

            if (Input.GetKeyDown(KeyCode.Slash) && !connectionFailed && !textBoxFocused)
            {
                GameObject panel = chatbox.transform.GetChild(0).gameObject;
                GameObject inputFieldObject = panel.transform.GetChild(1).gameObject;
                GameObject placeholderText = inputFieldObject.transform.GetChild(2).gameObject;

                input.GetComponent<UnityEngine.UI.Text>().text = " |";
                input.GetComponent<UnityEngine.UI.Text>().color = Color.white;
                FindAnyObjectByType<ac_CharacterController>().ToggleFreezePlayer(false);
                GameManager.GM.TimeStopped = true;

                textBoxFocused = true;

                input.GetComponent<UnityEngine.UI.Text>().material = messages.GetComponent<UnityEngine.UI.Text>().material;

            }
            else if (Input.GetKeyDown(KeyCode.Slash) && textBoxFocused && !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
            {
                input.GetComponent<UnityEngine.UI.Text>().text = "Press / to start typing...";
                input.GetComponent<UnityEngine.UI.Text>().color = Color.gray;
                FindAnyObjectByType<ac_CharacterController>().ToggleFreezePlayer(false);
                GameManager.GM.TimeStopped = false;

                textBoxFocused = false;

                input.GetComponent<UnityEngine.UI.Text>().material = defaultMateral;
            }
            else if (textBoxFocused)
            {
                processKeys();
            }

            if (connectionAttempted)
            {
                if (!client.Connected && !connectionFailed)
                {
                    if (input.GetComponent<UnityEngine.UI.Text>().text == "Connection lost") return;

                    input.GetComponent<UnityEngine.UI.Text>().text = "Connection lost";
                    input.GetComponent<UnityEngine.UI.Text>().material = defaultMateral;
                    input.GetComponent<UnityEngine.UI.Text>().color = Color.gray;
                    FindAnyObjectByType<ac_CharacterController>().ToggleFreezePlayer(true);
                } else
                {
                    checkForMessages(chatbox);
                }
            }
            else
            {
                connect();

                if (connectionFailed)
                {
                    input.transform.GetComponent<UnityEngine.UI.Text>().text = "Failed to connect to server";
                }
                else
                {
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes($"{PluginInfo.PLUGIN_VERSION}~{username} connected");
                    stream.Write(buffer, 0, buffer.Length);
                    stream.FlushAsync();
                }
            }
        }

        private GameObject createChatBox()
        {
            if (bundle != null)
            {
                bundle.Unload(false);
            }


            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream fontStream = assembly.GetManifestResourceStream("Client.ChakraPetch-Regular.ttf");

            byte[] fontData = new byte[fontStream.Length];
            fontStream.Read(fontData, 0, (int)fontStream.Length);
            fontStream.Close();

            string tempFontPath = Path.Combine(Application.temporaryCachePath, "ChakraPetch-Regular.ttf");
            File.WriteAllBytes(tempFontPath, fontData);

            font = Font.CreateDynamicFontFromOSFont(tempFontPath, 14);

            File.Delete(tempFontPath);

            Stream spriteStream = assembly.GetManifestResourceStream("Client.chatbox.sprite");

            byte[] spriteData = new byte[spriteStream.Length];
            spriteStream.Read(spriteData, 0, (int)spriteStream.Length);
            spriteStream.Close();

            string tempSpritePath = Path.Combine(Application.temporaryCachePath, "chatbox.sprite");
            File.WriteAllBytes(tempSpritePath, spriteData);

            bundle = AssetBundle.LoadFromFile(tempSpritePath);
            sprite = bundle.LoadAsset<Sprite>("Chatbox");

            File.Delete(tempSpritePath);

            GameObject chatbox = new GameObject("Chatbox");
            chatbox.AddComponent<RectTransform>();
            chatbox.AddComponent<Canvas>();
            chatbox.AddComponent<UnityEngine.UI.CanvasScaler>();
            chatbox.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            chatbox.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 150);

            chatbox.transform.localScale = new Vector3(1, 1, 1);
            chatbox.transform.localPosition = new Vector3(1, 1, 1);
            chatbox.layer = LayerMask.NameToLayer("UI");

            GameObject panel = new GameObject("Panel");

            panel.AddComponent<RectTransform>();
            panel.transform.SetParent(chatbox.transform);
            panel.GetComponent<RectTransform>().sizeDelta = new Vector2(300, 200);
            panel.layer = LayerMask.NameToLayer("UI");
            panel.AddComponent<UnityEngine.UI.Image>();
            panel.GetComponent<UnityEngine.UI.Image>().sprite = sprite;

            UnityEngine.UI.Image imageComponent = panel.GetComponent<UnityEngine.UI.Image>();
            Color currentColor = imageComponent.color;
            currentColor.a = 0.9961f;
            imageComponent.color = currentColor;

            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(panel.transform);
            textObject.layer = LayerMask.NameToLayer("UI");
            UnityEngine.UI.Text textComponent = textObject.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = "";
            textComponent.font = font;
            textComponent.fontSize = 80;
            textComponent.alignment = TextAnchor.LowerLeft;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;

            defaultMateral = textComponent.material;

            textComponent.material = hud.transform.GetChild(6).GetChild(3).gameObject.GetComponent<UnityEngine.UI.Text>().material;
            this.messages = textObject;

            GameObject inputFieldObject = new GameObject("InputField");
            inputFieldObject.transform.SetParent(panel.transform);
            inputFieldObject.layer = LayerMask.NameToLayer("UI");
            UnityEngine.UI.InputField inputField = inputFieldObject.AddComponent<UnityEngine.UI.InputField>();

            GameObject inputFieldTextObject = new GameObject("Text");
            inputFieldTextObject.transform.SetParent(inputFieldObject.transform);
            UnityEngine.UI.Text inputFieldText = inputFieldTextObject.AddComponent<UnityEngine.UI.Text>();
            inputField.textComponent = inputFieldText;

            inputFieldText.color = Color.white;
            inputFieldText.font = font;
            inputFieldText.fontSize = 80;
            inputFieldText.verticalOverflow = VerticalWrapMode.Overflow;
            inputFieldText.horizontalOverflow = HorizontalWrapMode.Overflow;

            GameObject placeholderTextObject = new GameObject("Placeholder");
            placeholderTextObject.transform.SetParent(inputFieldObject.transform);
            UnityEngine.UI.Text placeholderText = placeholderTextObject.AddComponent<UnityEngine.UI.Text>();
            inputField.placeholder = placeholderText;

            placeholderText.text = "Press / to start typing...";
            placeholderText.color = Color.gray;
            placeholderText.font = font;
            placeholderText.fontSize = 80;
            placeholderText.verticalOverflow = VerticalWrapMode.Overflow;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Overflow;
            this.input = placeholderTextObject;

            return chatbox;
        }


        private string getSteamUsername()
        {
            string keyName = @"SOFTWARE\Valve\Steam";
            using (RegistryKey key = Registry.CurrentUser.OpenSubKey(keyName))
            {
                if (key != null)
                {
                    object lastGameNameUsed = key.GetValue("LastGameNameUsed");
                    if (lastGameNameUsed != null)
                    {
                        return lastGameNameUsed.ToString();
                    }
                }
            }
            return null;
        }

        private void connect()
        {
            try
            {
                client = new TcpClient("192.9.134.179", 80);
                stream = client.GetStream();
            }
            catch
            {
                connectionFailed = true;
                Logger.LogInfo("Failed to connect to server");
            }
            connectionAttempted = true;
        }

        private void checkForMessages(GameObject chatbox)
        {
            if (connectionFailed)
            {
                return;
            }

            while (stream.DataAvailable)
            {
                byte[] buffer = new byte[1024];
                int bytesRead = stream.Read(buffer, 0, buffer.Length);
                string message = System.Text.Encoding.UTF8.GetString(buffer, 0, bytesRead);
                Logger.LogInfo($"Received message: {message}");

                message = SplitLine(message, 165, font, 8);

                string existingText = messages.transform.GetComponent<UnityEngine.UI.Text>().text;

                List<string> lines = new List<string>(existingText.Split('\n'));
                List<string> messageLines = new List<string>(message.Split('\n'));

                lines.AddRange(messageLines);

                if (lines.Count > 10)
                {
                    lines = lines.Skip(lines.Count - 10).ToList();
                }

                string newText = string.Join("\n", lines);

                messages.transform.GetComponent<UnityEngine.UI.Text>().text = newText;

            }
        }

        private string SplitLine(string text, float maxLineWidth, Font font, int fontSize)
        {
            var words = text.Split(' ');
            var lines = new List<string>();
            var currentLine = "";
            var textGenerator = new TextGenerator();

            var settings = new TextGenerationSettings
            {
                font = font,
                fontSize = fontSize,
                scaleFactor = 1,
                color = Color.white,
                fontStyle = FontStyle.Normal,
                verticalOverflow = VerticalWrapMode.Overflow,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                generationExtents = new Vector2(10000, 10000),
                pivot = Vector2.zero,
                richText = true,
                lineSpacing = 1,
                alignByGeometry = false,
                resizeTextForBestFit = false,
                updateBounds = false,
                textAnchor = TextAnchor.UpperLeft
            };

            foreach (var word in words)
            {
                var potentialLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                textGenerator.Populate(potentialLine, settings);
                var lineWidth = textGenerator.GetPreferredWidth(potentialLine, settings);

                if (lineWidth > maxLineWidth)
                {
                    textGenerator.Populate(word, settings);
                    var wordWidth = textGenerator.GetPreferredWidth(word, settings);

                    if (wordWidth > maxLineWidth)
                    {
                        var splitWord = SplitWord(word, maxLineWidth, font, fontSize);
                        lines.AddRange(splitWord);
                    }
                    else
                    {
                        lines.Add(currentLine);
                        currentLine = word;
                    }
                }
                else
                {
                    currentLine = potentialLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return string.Join("\n", lines);
        }

        private List<string> SplitWord(string word, float maxLineWidth, Font font, int fontSize)
        {
            var textGenerator = new TextGenerator();
            var settings = new TextGenerationSettings
            {
                font = font,
                fontSize = fontSize,
                scaleFactor = 1,
                color = Color.white,
                fontStyle = FontStyle.Normal,
                verticalOverflow = VerticalWrapMode.Overflow,
                horizontalOverflow = HorizontalWrapMode.Overflow,
                generationExtents = new Vector2(10000, 10000),
                pivot = Vector2.zero,
                richText = true,
                lineSpacing = 1,
                alignByGeometry = false,
                resizeTextForBestFit = false,
                updateBounds = false,
                textAnchor = TextAnchor.UpperLeft
            };

            var splitWord = new List<string>();
            var currentPart = "";

            for (int i = 0; i < word.Length; i++)
            {
                var potentialPart = currentPart + word[i];
                textGenerator.Populate(potentialPart, settings);
                var partWidth = textGenerator.GetPreferredWidth(potentialPart, settings);

                if (partWidth > maxLineWidth)
                {
                    splitWord.Add(currentPart);
                    currentPart = word[i].ToString();
                }
                else
                {
                    currentPart = potentialPart;
                }
            }

            if (!string.IsNullOrEmpty(currentPart))
            {
                splitWord.Add(currentPart);
            }

            return splitWord;
        }
        public void processKeys()
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {

                input.GetComponent<UnityEngine.UI.Text>().text = input.GetComponent<UnityEngine.UI.Text>().text.Replace(" |", "");
                if (input.GetComponent<UnityEngine.UI.Text>().text != "")
                {
                    string message = $"{username}: {input.GetComponent<UnityEngine.UI.Text>().text}";
                    message = message.Replace(" \n", " ");
                    message = message.Replace("\n", " ");

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes($"{PluginInfo.PLUGIN_VERSION}~{message}");
                    stream.Write(buffer, 0, buffer.Length);
                    stream.FlushAsync();
                }

                input.GetComponent<UnityEngine.UI.Text>().text = "Press / to start typing...";
                input.GetComponent<UnityEngine.UI.Text>().color = Color.gray;
                FindAnyObjectByType<ac_CharacterController>().ToggleFreezePlayer(true);
                GameManager.GM.TimeStopped = false;
                textBoxFocused = false;

                input.GetComponent<UnityEngine.UI.Text>().material = defaultMateral;
            }


            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                input.GetComponent<UnityEngine.UI.Text>().text = input.GetComponent<UnityEngine.UI.Text>().text.Replace(" |", "");
                string currentText = input.GetComponent<UnityEngine.UI.Text>().text;
                if (currentText.Length > 0)
                {
                    input.GetComponent<UnityEngine.UI.Text>().text = currentText.Substring(0, currentText.Length - 1);

                        input.GetComponent<UnityEngine.UI.Text>().text += " |";
                }

                string[] lines = input.GetComponent<UnityEngine.UI.Text>().text.Split('\n');
            }

            if (Input.anyKeyDown)
            {
                foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(keyCode))
                    {
                        string keyString = keyCode.ToString();

                        if (keyString.StartsWith("Alpha"))
                        {
                            keyString = keyString.Substring(5);
                        }
                        else if (keyString.StartsWith("Keypad"))
                        {
                            keyString = keyString.Substring(6);
                        }
                        else if (keyString == "Space")
                        {
                            keyString = " ";
                        }
                        else if (keyString.Length > 1)
                        {
                            switch (keyString)
                            {
                                case "Period":
                                    keyString = ".";
                                    break;
                                case "Comma":
                                    keyString = ",";
                                    break;
                                case "Slash":
                                    keyString = "/";
                                    break;
                                case "Backslash":
                                    keyString = "\\";
                                    break;
                                case "Minus":
                                    keyString = "-";
                                    break;
                                case "Equals":
                                    keyString = "=";
                                    break;
                                case "Semicolon":
                                    keyString = ";";
                                    break;
                                case "Quote":
                                    keyString = "'";
                                    break;
                                default:
                                    continue;
                            }
                        }

                        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.CapsLock))
                        {
                            keyString = keyString.ToUpper();

                            switch (keyString)
                            {
                                case ".":
                                    keyString = ">";
                                    break;
                                case ",":
                                    keyString = "<";
                                    break;
                                case "/":
                                    keyString = "?";
                                    break;
                                case "\\":
                                    keyString = "|";
                                    break;
                                case "-":
                                    keyString = "_";
                                    break;
                                case "=":
                                    keyString = "+";
                                    break;
                                case ";":
                                    keyString = ":";
                                    break;
                                case "'":
                                    keyString = "\"";
                                    break;
                                case "1":
                                    keyString = "!";
                                    break;
                                case "2":
                                    keyString = "@";
                                    break;
                                case "3":
                                    keyString = "#";
                                    break;
                                case "4":
                                    keyString = "$";
                                    break;
                                case "5":
                                    keyString = "%";
                                    break;
                                case "6":
                                    keyString = "^";
                                    break;
                                case "7":
                                    keyString = "&";
                                    break;
                                case "8":
                                    keyString = "*";
                                    break;
                                case "9":
                                    keyString = "(";
                                    break;
                                case "0":
                                    keyString = ")";
                                    break;
                            }
                        }
                        else
                        {
                            keyString = keyString.ToLower();
                        }

                        input.GetComponent<UnityEngine.UI.Text>().text = input.GetComponent<UnityEngine.UI.Text>().text.Replace(" |", "");

                        string newText = input.GetComponent<UnityEngine.UI.Text>().text + keyString;

                        newText = SplitLine(newText, 165, font, 8);

                        string[] lines = newText.Split('\n');

                        if (lines.Length < 3)
                        {
                            newText += " |";

                            input.GetComponent<UnityEngine.UI.Text>().text = newText;
                        }
                        break;
                    }
                }
            }
        }
    }
}