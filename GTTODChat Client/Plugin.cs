﻿using System;
using Microsoft.Win32;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using System.Net.Sockets;
using UnityEngine.UIElements;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;

namespace Client
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private bool connectionAttempted = false;
        private bool connectionFailed = false;
        private TcpClient client;
        private NetworkStream stream;

        private GameObject hud;

        private bool textBoxFocused = false;

        private Font font;

        private  string username;

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
        }

        private void Update()
        {
            hud = GameObject.Find("PlayerHUD");
            if (hud == null) return;

            GameObject chatbox = GameObject.Find("Chatbox");
            if (chatbox == null)
            {
                chatbox = createChatBox();

                chatbox.transform.SetParent(hud.transform);
                chatbox.transform.localPosition = new Vector3(600, 325, 0);
                chatbox.transform.localScale = new Vector3(3, 2, 1);
                chatbox.transform.localRotation = Quaternion.Euler(356.5f, 3.5f, 0);

                GameObject panel = chatbox.transform.Find("Panel").gameObject;
                panel.transform.localPosition = new Vector3(0, 0, 0);
                panel.transform.localScale = new Vector3(1, 1, 1);

                GameObject newLineBacking = panel.transform.Find("NewLineBacking").gameObject;
                newLineBacking.transform.localPosition = new Vector3(0, -55, 0);
                newLineBacking.transform.localScale = new Vector3(1, .1f, 1);
                newLineBacking.SetActive(false);

                GameObject textObject = panel.transform.Find("Text").gameObject;
                textObject.transform.localPosition = new Vector3(-44, -34, 0);
                textObject.transform.localScale = new Vector3(.1f, .1f, 1);

                GameObject inputFieldObject = panel.transform.Find("InputField").gameObject;
                inputFieldObject.transform.localPosition = new Vector3(-44, -45, 0);
                inputFieldObject.transform.localScale = new Vector3(.1f, .1f, 1);
            }

            if (Input.GetKeyDown(KeyCode.Slash) && !connectionFailed && !textBoxFocused)
            {
                GameObject panel = chatbox.transform.Find("Panel").gameObject;
                GameObject inputFieldObject = panel.transform.Find("InputField").gameObject;
                GameObject placeholderText = inputFieldObject.transform.Find("Placeholder").gameObject;

                GameObject GTTOD = GameObject.Find("GTTOD").gameObject;
                GameObject Player = GameObject.Find("Player").gameObject;

                UnityEngine.UI.InputField inputField = inputFieldObject.GetComponent<UnityEngine.UI.InputField>();

                placeholderText.GetComponent<UnityEngine.UI.Text>().text = " |"; // Clear the placeholder text
                placeholderText.GetComponent<UnityEngine.UI.Text>().color = Color.white;
                Player.gameObject.GetComponent<ac_CharacterController>().ToggleFreezePlayer(false);
                GTTOD.gameObject.GetComponent<GameManager>().TimeStopped = true;

                textBoxFocused = true;
            } else if (Input.GetKeyDown(KeyCode.Slash) && textBoxFocused)
            {
                GameObject panel = chatbox.transform.Find("Panel").gameObject;
                GameObject inputFieldObject = panel.transform.Find("InputField").gameObject;
                GameObject placeholderText = inputFieldObject.transform.Find("Placeholder").gameObject;
                GameObject GTTOD = GameObject.Find("GTTOD").gameObject;
                GameObject Player = GameObject.Find("Player").gameObject;
                GameObject newLineBacking = panel.transform.Find("NewLineBacking").gameObject;

                newLineBacking.SetActive(false);
                placeholderText.GetComponent<UnityEngine.UI.Text>().text = "Press / to start typing..."; // Restore the placeholder text
                placeholderText.GetComponent<UnityEngine.UI.Text>().color = Color.gray;
                Player.gameObject.GetComponent<ac_CharacterController>().ToggleFreezePlayer(true);
                GTTOD.gameObject.GetComponent<GameManager>().TimeStopped = false;

                textBoxFocused = false;
            } else if (textBoxFocused)
            {
                processKeys();
            }

            if (connectionAttempted)
            {
                checkForMessages(chatbox);
            }
            else
            {
                connect();

                GameObject panel = chatbox.transform.Find("Panel").gameObject;
                GameObject textObject = panel.transform.Find("Text").gameObject;
                if (connectionFailed)
                {
                    textObject.transform.GetComponent<UnityEngine.UI.Text>().text = "Failed to connect to server";
                }
                else
                {
                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes($"{username} connected");
                    stream.Write(buffer, 0, buffer.Length);
                    stream.FlushAsync();
                }
            }
        }

        private GameObject createChatBox()
        {
            Assembly assembly = Assembly.GetExecutingAssembly();
            Stream fontStream = assembly.GetManifestResourceStream("Client.ChakraPetch-Regular.ttf");

            byte[] fontData = new byte[fontStream.Length];
            fontStream.Read(fontData, 0, (int)fontStream.Length);
            fontStream.Close();

            // Create a temporary file to hold the font data
            string tempFontPath = Path.Combine(Application.temporaryCachePath, "ChakraPetch-Regular.ttf");
            File.WriteAllBytes(tempFontPath, fontData);

            // Load the font from the temporary file
            font = Font.CreateDynamicFontFromOSFont(tempFontPath, 14);

            // Delete the temporary file
            File.Delete(tempFontPath);


            // Create the chat box
            GameObject chatbox = new GameObject("Chatbox");
            chatbox.AddComponent<Canvas>();
            chatbox.AddComponent<UnityEngine.UI.CanvasScaler>();
            chatbox.AddComponent<UnityEngine.UI.GraphicRaycaster>();
            chatbox.transform.localScale = new Vector3(1, 1, 1);
            chatbox.transform.localPosition = new Vector3(1, 1, 1);
            chatbox.layer = LayerMask.NameToLayer("UI");

            // Create a panel as a child of the chat box
            GameObject panel = new GameObject("Panel");
            panel.transform.SetParent(chatbox.transform);
            panel.layer = LayerMask.NameToLayer("UI");
            UnityEngine.UI.Image panelImage = panel.AddComponent<UnityEngine.UI.Image>();
            panelImage.color = new Color(0, 0, 0, 0.9f); // Set the color to black with 50% transparency

            GameObject newLineBacking = new GameObject("NewLineBacking");
            newLineBacking.transform.SetParent(panel.transform);
            newLineBacking.layer = LayerMask.NameToLayer("UI");
            UnityEngine.UI.Image newLineBackingImage = newLineBacking.AddComponent<UnityEngine.UI.Image>();
            newLineBackingImage.color = new Color(0, 0, 0, 0.9f); // Set the color to black with 50% transparency

            // Create a text component as a child of the panel for chat messages
            GameObject textObject = new GameObject("Text");
            textObject.transform.SetParent(panel.transform);
            textObject.layer = LayerMask.NameToLayer("UI");
            UnityEngine.UI.Text textComponent = textObject.AddComponent<UnityEngine.UI.Text>();
            textComponent.text = ""; // Initialize the text component with an empty string
            textComponent.font = font;
            textComponent.fontSize = 80;
            textComponent.alignment = TextAnchor.LowerLeft;
            textComponent.verticalOverflow = VerticalWrapMode.Overflow;
            textComponent.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Create an input field as a child of the panel for text entry
            GameObject inputFieldObject = new GameObject("InputField");
            inputFieldObject.transform.SetParent(panel.transform);
            inputFieldObject.layer = LayerMask.NameToLayer("UI");
            UnityEngine.UI.InputField inputField = inputFieldObject.AddComponent<UnityEngine.UI.InputField>();

            // Create a Text object for the input field's text
            GameObject inputFieldTextObject = new GameObject("Text");
            inputFieldTextObject.transform.SetParent(inputFieldObject.transform);
            UnityEngine.UI.Text inputFieldText = inputFieldTextObject.AddComponent<UnityEngine.UI.Text>();
            inputField.textComponent = inputFieldText;

            // Set the color and font of the input field's text
            inputFieldText.color = Color.white; // Set the text color to white
            inputFieldText.font = font;
            inputFieldText.fontSize = 80;
            inputFieldText.verticalOverflow = VerticalWrapMode.Overflow;
            inputFieldText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // Create a Text object for the input field's placeholder text
            GameObject placeholderTextObject = new GameObject("Placeholder");
            placeholderTextObject.transform.SetParent(inputFieldObject.transform);
            UnityEngine.UI.Text placeholderText = placeholderTextObject.AddComponent<UnityEngine.UI.Text>();
            inputField.placeholder = placeholderText;

            // Set the color and font of the input field's placeholder text
            placeholderText.text = "Press / to start typing...";
            placeholderText.color = Color.gray; // Set the placeholder text color to gray
            placeholderText.font = font;
            placeholderText.fontSize = 80;
            placeholderText.verticalOverflow = VerticalWrapMode.Overflow;
            placeholderText.horizontalOverflow = HorizontalWrapMode.Overflow;

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

                message = SplitLine(message, 90, font, 8);

                GameObject panel = chatbox.transform.Find("Panel").gameObject;
                GameObject textObject = panel.transform.Find("Text").gameObject;
                string existingText = textObject.transform.GetComponent<UnityEngine.UI.Text>().text;

                List<string> lines = new List<string>(existingText.Split('\n'));
                List<string> messageLines = new List<string>(message.Split('\n'));

                lines.AddRange(messageLines);

                if (lines.Count > 10)
                {
                    lines = lines.Skip(lines.Count - 10).ToList();
                }

                string newText = string.Join("\n", lines);

                textObject.transform.GetComponent<UnityEngine.UI.Text>().text = newText;

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
                    // Check if the word itself is longer than maxLineWidth
                    textGenerator.Populate(word, settings);
                    var wordWidth = textGenerator.GetPreferredWidth(word, settings);

                    if (wordWidth > maxLineWidth)
                    {
                        // If the word is longer than maxLineWidth, split it
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
            GameObject chatbox = GameObject.Find("Chatbox");
            GameObject panel = chatbox.transform.Find("Panel").gameObject;
            GameObject inputFieldObject = panel.transform.Find("InputField").gameObject;
            GameObject placeholderText = inputFieldObject.transform.Find("Placeholder").gameObject;
            GameObject newLineBacking = panel.transform.Find("NewLineBacking").gameObject;


            GameObject GTTOD = GameObject.Find("GTTOD").gameObject;
            GameObject Player = GameObject.Find("Player").gameObject;
            if (Input.GetKeyDown(KeyCode.Return))
            {

                placeholderText.GetComponent<UnityEngine.UI.Text>().text = placeholderText.GetComponent<UnityEngine.UI.Text>().text.Replace(" |", "");
                if (placeholderText.GetComponent<UnityEngine.UI.Text>().text != "")
                {
                    string message = $"{username}: {placeholderText.GetComponent<UnityEngine.UI.Text>().text}";
                    message = message.Replace(" \n", " ");
                    message = message.Replace("\n", " ");

                    byte[] buffer = System.Text.Encoding.UTF8.GetBytes(message);
                    stream.Write(buffer, 0, buffer.Length);
                    stream.FlushAsync();
                }

                placeholderText.GetComponent<UnityEngine.UI.Text>().text = "Press / to start typing..."; // Restore the placeholder text
                placeholderText.GetComponent<UnityEngine.UI.Text>().color = Color.gray;
                Player.gameObject.GetComponent<ac_CharacterController>().ToggleFreezePlayer(true);
                GTTOD.gameObject.GetComponent<GameManager>().TimeStopped = false;
                newLineBacking.SetActive(false);
                textBoxFocused = false;
            }


            // If the backspace key is pressed, remove the last character from the placeholder text
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                placeholderText.GetComponent<UnityEngine.UI.Text>().text = placeholderText.GetComponent<UnityEngine.UI.Text>().text.Replace(" |", "");
                string currentText = placeholderText.GetComponent<UnityEngine.UI.Text>().text;
                if (currentText.Length > 0)
                {
                    placeholderText.GetComponent<UnityEngine.UI.Text>().text = currentText.Substring(0, currentText.Length - 1);

                    placeholderText.GetComponent<UnityEngine.UI.Text>().text += " |";
                }
            }

            if (Input.anyKeyDown)
            {
                foreach (KeyCode keyCode in Enum.GetValues(typeof(KeyCode)))
                {
                    if (Input.GetKeyDown(keyCode))
                    {
                        string keyString = keyCode.ToString();

                        // Convert KeyCode to actual string
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
                            // Handle special keys
                            switch (keyString)
                            {
                                case "Period":
                                    keyString = ".";
                                    break;
                                case "Comma":
                                    keyString = ",";
                                    break;
                                case "Slash":
                                    if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift))
                                    {

                                    }
                                    else
                                    {
                                        keyString = "/";
                                    }
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
                                    // Ignore other non-character keys
                                    continue;
                            }
                        }

                        if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift) || Input.GetKey(KeyCode.CapsLock))
                        {
                            keyString = keyString.ToUpper();

                            // Handle shift-modified keys
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
                            }
                        }
                        else
                        {
                            keyString = keyString.ToLower();
                        }

                        // Add the key to the placeholder text
                        placeholderText.GetComponent<UnityEngine.UI.Text>().text = placeholderText.GetComponent<UnityEngine.UI.Text>().text.Replace(" |", "");

                        string newText = placeholderText.GetComponent<UnityEngine.UI.Text>().text + keyString;

                        newText = SplitLine(newText, 90, font, 8);

                        string[] lines = newText.Split('\n');

                        if (lines.Length < 2)
                        {
                            newLineBacking.SetActive(false);
                        }
                        else
                        {
                            newLineBacking.SetActive(true);
                        }

                        if (lines.Length < 3)
                        {
                            newText += " |";

                            placeholderText.GetComponent<UnityEngine.UI.Text>().text = newText;
                        }
                        break;
                    }
                }
            }
        }
    }
}