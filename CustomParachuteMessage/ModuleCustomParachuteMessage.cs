using KSP;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Linq;

namespace CustomParachuteMessage
{
    public class ModuleCustomParachuteMessage : PartModule
    {
        private static AssetBundle resourcesAB;
        public static AssetBundle ResourcesAB
        {
            private set
            {
                resourcesAB = value;
            }
            get
            {
                if (resourcesAB == null)
                {
                    string directory = Path.Combine(KSPUtil.ApplicationRootPath, "GameData", 
                        "CustomParachuteMessage", "Assets");

                    string name = string.Empty;
                    switch (Application.platform)
                    {
                        case RuntimePlatform.OSXPlayer:
                            name = "resources_osx.assetbundle";
                            break;
                        case RuntimePlatform.WindowsPlayer:
                            name = "resources_win64.assetbundle";
                            break;
                        case RuntimePlatform.LinuxPlayer:
                            name = "resources_linux64.assetbundle";
                            break;
                        default:
                            // YOU WHAT
                            break;
                    }

                    string path = Path.Combine(directory, name);

                    if (!File.Exists(path))
                    {
                        Debug.LogError($"[Custom Parachute Message] Assetbundle {path} is " +
                            $"missing!");
                        return null;
                    }

                    resourcesAB = AssetBundle.LoadFromFile(path);

                    if (resourcesAB == null) // just to be extra safe
                    {
                        Debug.LogError($"[Custom Parachute Message] Error when loading {path}!");
                        return null;
                    }
                }
                return resourcesAB;
            }
        }

        // Not using, but it might be useful in the future
        private List<Transform> parachuteModel;

        #region Message
        // default: Dare Mighty Things 34d 11m 58s N 118d 10m 31s W
        [KSPField(isPersistant = true)] public string word_1 = "Dare";
        [KSPField(isPersistant = true)] public string word_2 = "Mighty";
        [KSPField(isPersistant = true)] public string word_3 = "Things";
        [KSPField(isPersistant = true)] public string word_4 = "";

        [KSPField(isPersistant = true)] public bool ring4IsCoordinate = true;

        [KSPField(isPersistant = true)] public int c1 = 34;
        [KSPField(isPersistant = true)] public int c2 = 11;
        [KSPField(isPersistant = true)] public int c3 = 58;
        [KSPField(isPersistant = true)] public string w1 = "N";
        [KSPField(isPersistant = true)] public int c4 = 118;
        [KSPField(isPersistant = true)] public int c5 = 10;
        [KSPField(isPersistant = true)] public int c6 = 31;
        [KSPField(isPersistant = true)] public string w2 = "W";

        // 16bit data serialized
        [KSPField(isPersistant = true)] public string dataStore = "00000000000000000000000000000" +
            "000000000000000000000000000000000000000000000000000";
        [KSPField(isPersistant = true)] public string color1Store = "#FFFFFF";
        [KSPField(isPersistant = true)] public string color2Store = "#FF6B00";
        [KSPField(isPersistant = true)] public bool bitMode;
        #endregion

        #region UI Variable
        private float[] data = new float[320];
        private bool pickingColor1 = false;
        private bool pickingColor2 = false;
        private int mode;
        private Rect windowRect = new Rect(100, 100, 300, 450);
        private bool showWindow = false;
        private Vector2 scollPosition;
        private bool autoUpdate = true;
        private string inputCode;
        private bool flatModel = true;
        private Transform previewModelTransform;
        private bool updated = false;
        #endregion

        public override void OnStart(StartState state)
        {
            base.OnStart(state);

            // deserialize the data
            SplitToArray(HexToBin(dataStore));
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            // just to make sure it will run after real chute
            if (updated)
            {
                return;
            }
            updated = true;

            Setup();
        }

        private void OnDestroy()
        {
            // make sure preview model gets destory
            if (previewModelTransform)
            {
                if (previewModelTransform.gameObject)
                {
                    DestroyImmediate(previewModelTransform.gameObject);
                }
            }
        }

        private void Setup()
        {
            Debug.Log($"[Custom Parachute Message] Replacing material");

            parachuteModel = new List<Transform>();

            Shader chuteShader = ResourcesAB.LoadAsset<Shader>("MessageChute");
            if (!chuteShader)
                Debug.LogError($"[Custom Parachute Message] Chute shader is missing in " +
                    $"asset bundle!");

            Shader ropeShader = ResourcesAB.LoadAsset<Shader>("Rope");
            if (!ropeShader)
                Debug.LogError($"[Custom Parachute Message] Rope shader is missing in " +
                    $"asset bundle!");

            Material chuteMaterial = ResourcesAB.LoadAsset<Material>("Chute");
            if (!chuteMaterial)
                Debug.LogError($"[Custom Parachute Message] Chute material is missing in " +
                    $"asset bundle!");
            Material chuteMaterial_clone = new Material(chuteMaterial);

            Material ropeMaterial = ResourcesAB.LoadAsset<Material>("RopeMat");
            if (!ropeMaterial)
                Debug.LogError($"[Custom Parachute Message] Rope material is missing in " +
                    $"asset bundle!");


            // So there is no elegant way for me to know what parachute model is in use because
            // Real Chute set all related fields internal.
            // Therefore I have to do name matching to find if my model is in use

            foreach (Transform child in GetAllChildren(transform))
            {
                // hey that's my boi
                if (child.name.Contains("model_canopyWithMessage"))
                {
                    // Fix real chute scale because why the parent transform has asymmetric scale
                    // The y will be different from x and z, resulting a elliptical chute
                    // WHY???

                    Vector3 parentScale = child.parent.transform.localScale;
                    if (parentScale.x == parentScale.z && parentScale.y != parentScale.x)
                    {
                        Transform parent = child.parent;
                        parentScale.y = parentScale.x;
                        child.parent = parent.parent;
                        parent.transform.localScale = parentScale;
                        child.parent = parent;
                        child.localScale = Vector3.one * child.localScale.x;

                        Debug.Log($"[Custom Parachute Message] Real chute scale fixed on " +
                            $"{parent.name}");
                    }

                    // now we can replace the material

                    foreach (Transform chutePart in GetAllChildren(child))
                    {
                        switch (chutePart.name)
                        {
                            case "rope":
                                chutePart.GetComponent<MeshRenderer>()
                                    .sharedMaterial = ropeMaterial;
                                break;
                            case "rope_up":
                                chutePart.GetComponent<MeshRenderer>()
                                    .sharedMaterial = ropeMaterial;
                                break;
                            case "canopy":
                                chutePart.GetComponent<MeshRenderer>()
                                    .sharedMaterial = chuteMaterial_clone;
                                break;
                            default:
                                break;
                        }
                    }

                    parachuteModel.Add(child);
                }
            }

            if (parachuteModel.Count == 0)
            {
                Debug.Log($"[Custom Parachute Message] Found no compatible model");
            }

            // validating parameter
            ValidateParameter();

            // update material
            chuteMaterial_clone.SetFloatArray("data", bitMode ? data : Encode());
            chuteMaterial_clone.SetColor("_color_A", HexToColor(color1Store));
            chuteMaterial_clone.SetColor("_color_B", HexToColor(color2Store));

            Debug.Log($"[Custom Parachute Message] All done!");
        }

        // Editor customization window button
        [KSPEvent(guiActiveEditor = true, guiName = "Toggle Customization Window", active = true)]
        public void ToggleEditWindowEvent()
        {
            if (showWindow)
                CloseWindow();
            else
                OpenWindow();
        }

        #region Encode Algorithm

        private float[] Encode()
        {
            string s = string.Empty;
            int o = 0;
            o = EncodeWord(ref s, word_1, o);
            o = EncodeWord(ref s, word_2, o);
            o = EncodeWord(ref s, word_3, o);

            if (ring4IsCoordinate)
            {
                EncodeCoordinate(ref s, o);
            }
            else
            {
                EncodeWord(ref s, word_4, o);
            }

            float[] data = new float[320];
            for (int i = 0; i < s.Length; i++)
            {
                data[i] = int.Parse(s[i].ToString());
            }

            return data;
        }

        private void EncodeCoordinate(ref string s, int offset)
        {
            FixLength(ref w1, 1);
            FixLength(ref w2, 1);
            StringBuilder sb = new StringBuilder();
            sb.Append((char)c1);
            sb.Append((char)c2);
            sb.Append((char)c3);
            sb.Append((char)(char.ToUpper(w1[0]) - 64));
            sb.Append((char)c4);
            sb.Append((char)c5);
            sb.Append((char)c6);
            sb.Append((char)(char.ToUpper(w2[0]) - 64));
            string word = sb.ToString();
            string t = string.Empty;
            for (int i = 0; i < word.Length; i++)
            {
                char c = word[i];

                int n = c;
                string bin = System.Convert.ToString(n, 2);
                FixLength(ref bin, 10, '0', true);
                t += bin;
            }
            t = t.Substring(offset % t.Length, t.Length - offset % t.Length) + t.Substring(0, offset % t.Length);
            s += t;
        }

        private int EncodeWord(ref string s, string word, int offset)
        {
            string t = string.Empty;
            int ending = 0;
            for (int i = 0; i < word.Length; i++)
            {
                char c = word[i];

                if (c == ' ')
                {
                    if (i == 0)
                    {
                        t += new string('0', 80);
                        break;
                    }

                    t += new string('0', 3);
                    t += new string('1', 77 - (i * 10));

                    ending = (8 - i) * 10;

                    break;
                }
                else
                {
                    int n = char.ToUpper(c) - 64;
                    string bin = System.Convert.ToString(n, 2);
                    FixLength(ref bin, 10, '0', true);
                    t += bin;
                }
            }
            t = t.Substring(offset % t.Length, t.Length - offset % t.Length) + t.Substring(0, offset % t.Length);
            s += t;
            return ending + offset;
        }

        private void FixLength(ref string s, int targetLength, char fill = ' ', bool addToFront = false)
        {
            if (s.Length > targetLength)
                s = s.Substring(0, targetLength);

            if (addToFront)
            {
                s = new string(fill, targetLength - s.Length) + s;
            }
            else
            {
                s += new string(fill, targetLength - s.Length);
            }
        }

        #endregion

        #region UI

        private void OpenWindow()
        {
            mode = bitMode ? 1 : 0;

            Debug.Log($"[Custom Parachute Message] Open window");
            showWindow = true;

            // load preview model
            GameObject previewModel = ResourcesAB.LoadAsset<GameObject>("PreviewModel");
            if (!previewModel)
                Debug.LogError($"[Custom Parachute Message] Preview Model is missing in " +
                    $"asset bundle!");

            previewModelTransform = Instantiate(previewModel).transform;

            SetPreviewModelTransform();
        }

        private void SetPreviewModelTransform()
        {
            if (previewModelTransform != null)
            {
                previewModelTransform.position = new Vector3(0, 25, flatModel ? 25 : 15);
                previewModelTransform.rotation = Quaternion.Euler(0, 0, 180);
                previewModelTransform.localScale = new Vector3(1, 1, flatModel ? 0.01f : 1f);
            } 
        }

        private void CloseWindow()
        {
            Debug.Log($"[Custom Parachute Message] Close Window");
            showWindow = false;
            DestroyImmediate(previewModelTransform.gameObject);
            Debug.Log($"[Custom Parachute Message] Preview Object Destroyed");
        }

        void OnGUI()
        {
            if (showWindow)
            {
                windowRect = GUILayout.Window((int)(part.persistentId - int.MaxValue / 2), windowRect, ProcessWindow, "Customize Parachute");
            }
        }

        void ProcessWindow(int windowID)
        {
            GUI.DragWindow(new Rect(0, 0, windowRect.width - 15, 20));

            #region Close button
            if (GUI.Button(new Rect(windowRect.width - 15, 3, 12, 12), ""))
            {
                CloseWindow();
                return;
            }        
            #endregion

            #region Color Picker
            Color oldColor = GUI.color;
            GUILayout.BeginHorizontal();
            GUILayout.Label("Color");
            GUI.color = HexToColor(color1Store);
            if (GUILayout.Button("Color A"))
            {
                pickingColor2 = false;
                pickingColor1 = !pickingColor1;
            }
            GUI.color = HexToColor(color2Store);
            if (GUILayout.Button("Color B"))
            {
                pickingColor1 = false;
                pickingColor2 = !pickingColor2;
            }
            GUILayout.EndHorizontal();
            GUI.color = oldColor;

            if (pickingColor1 || pickingColor2)
            {
                Color c = HexToColor(pickingColor1 ? color1Store : color2Store);

                GUILayout.Space(10);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Red", GUILayout.Width(50));
                float r = GUILayout.HorizontalSlider(c.r * 255, 0, 255, GUILayout.Width(150));
                GUILayout.Label(r.ToString("000"), GUILayout.Width(30));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Green", GUILayout.Width(50));
                float g = GUILayout.HorizontalSlider(c.g * 255, 0, 255, GUILayout.Width(150));
                GUILayout.Label(g.ToString("000"), GUILayout.Width(30));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Blue", GUILayout.Width(50));
                float b = GUILayout.HorizontalSlider(c.b * 255, 0, 255, GUILayout.Width(150));
                GUILayout.Label(b.ToString("000"), GUILayout.Width(30));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                Color n = new Color(r / 255f, g / 255f, b / 255f);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Hex", GUILayout.Width(50));
                string hex = GUILayout.TextField(ColorToHex(n), GUILayout.Width(180));
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                if (pickingColor1)
                    color1Store = hex;
                else
                    color2Store = hex;

                GUILayout.Space(10);
            }

            #endregion

            #region Editing Mode Picker
            GUILayout.BeginHorizontal();
            GUILayout.Label("Editing Mode");
            mode = GUILayout.Toolbar(mode, new string[] { "Standard", "Bitwise" });
            GUILayout.EndHorizontal();
            GUILayout.Space(10);
            #endregion

            #region Editing Area
            scollPosition = GUILayout.BeginScrollView(scollPosition);

            if (mode == 0) // standard mode
            {
                ring4IsCoordinate = GUILayout.Toggle(ring4IsCoordinate, "Ring 4 is coordinate");

                GUILayout.BeginHorizontal();
                GUILayout.Label("Word 1");
                word_1 = GUILayout.TextField(word_1, GUILayout.Width(150));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Word 2");
                word_2 = GUILayout.TextField(word_2, GUILayout.Width(150));
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Word 3");
                word_3 = GUILayout.TextField(word_3, GUILayout.Width(150));
                GUILayout.EndHorizontal();

                if (ring4IsCoordinate)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Latitude", GUILayout.Width(70));
                    string c1_s = GUILayout.TextField(c1.ToString(), GUILayout.Width(30));
                    int.TryParse(c1_s, out c1);
                    GUILayout.Label("°");
                    string c2_s = GUILayout.TextField(c2.ToString(), GUILayout.Width(30));
                    int.TryParse(c2_s, out c2);
                    GUILayout.Label("'");
                    string c3_s = GUILayout.TextField(c3.ToString(), GUILayout.Width(30));
                    int.TryParse(c3_s, out c3);
                    GUILayout.Label("\"");
                    w1 = GUILayout.TextField(w1.ToString(), GUILayout.Width(30));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Longitude", GUILayout.Width(70));
                    string c4_s = GUILayout.TextField(c4.ToString(), GUILayout.Width(30));
                    int.TryParse(c4_s, out c4);
                    GUILayout.Label("°");
                    string c5_s = GUILayout.TextField(c5.ToString(), GUILayout.Width(30));
                    int.TryParse(c5_s, out c5);
                    GUILayout.Label("'");
                    string c6_s = GUILayout.TextField(c6.ToString(), GUILayout.Width(30));
                    int.TryParse(c6_s, out c6);
                    GUILayout.Label("\"");
                    w2 = GUILayout.TextField(w2.ToString(), GUILayout.Width(30));
                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Word 4");
                    word_4 = GUILayout.TextField(word_4, GUILayout.Width(150));
                    GUILayout.EndHorizontal();

                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("<i>Non-canon!</i>");
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();
                }

                #region Presets
                GUILayout.Space(10);
                GUILayout.Label("Presets");
                if (GUILayout.Button("Perseverance"))
                {
                    word_1 = "Dare";
                    word_2 = "Mighty";
                    word_3 = "Things";
                    word_4 = "";
                    ring4IsCoordinate = true;
                    c1 = 34;
                    c2 = 11;
                    c3 = 58;
                    w1 = "N";
                    c4 = 118;
                    c5 = 10;
                    c6 = 31;
                    w2 = "W";
                }
                if (GUILayout.Button("KSP"))
                {
                    word_1 = "Kerbal";
                    word_2 = "Space";
                    word_3 = "Program";
                    word_4 = "KSP";
                    ring4IsCoordinate = false;
                    c1 = 0;
                    c2 = 0;
                    c3 = 0;
                    w1 = "N";
                    c4 = 0;
                    c5 = 0;
                    c6 = 0;
                    w2 = "E";
                }
                if (GUILayout.Button("KSC 1"))
                {
                    word_1 = "Kerbal";
                    word_2 = "Space";
                    word_3 = "Center";
                    word_4 = "";
                    ring4IsCoordinate = true;
                    c1 = 0;
                    c2 = 6;
                    c3 = 9;
                    w1 = "S";
                    c4 = 74;
                    c5 = 34;
                    c6 = 31;
                    w2 = "W";
                }
                if (GUILayout.Button("KSC 2"))
                {
                    word_1 = "Kennedy";
                    word_2 = "Space";
                    word_3 = "Center";
                    word_4 = "";
                    ring4IsCoordinate = true;
                    c1 = 28;
                    c2 = 34;
                    c3 = 24;
                    w1 = "N";
                    c4 = 80;
                    c5 = 39;
                    c6 = 4;
                    w2 = "W";
                }
                if (GUILayout.Button("Rocket Science"))
                {
                    word_1 = "HowHard";
                    word_2 = "CanRocke";
                    word_3 = "tScience";
                    word_4 = "BeAnyway";
                    ring4IsCoordinate = false;
                    c1 = 0;
                    c2 = 0;
                    c3 = 0;
                    w1 = "N";
                    c4 = 0;
                    c5 = 0;
                    c6 = 0;
                    w2 = "E";
                }

                #endregion
            }
            else if (mode == 1) // bit switch
            {
                string bin = CombineToBinString();
                string hex = BinToHex(bin);

                GUILayout.Label("Pattern Code");

                GUILayout.BeginHorizontal();
                GUILayout.TextField(hex, GUILayout.Width(150));

                if (GUILayout.Button("Copy"))
                {
                    TextEditor te = new TextEditor();
                    te.text = hex;
                    te.SelectAll();
                    te.Copy();
                }

                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                inputCode = GUILayout.TextField(inputCode, GUILayout.Width(150));

                if (GUILayout.Button("Load"))
                {
                    try
                    {
                        string binarystring = HexToBin(inputCode);
                        SplitToArray(binarystring);
                    }
                    catch
                    {
                        inputCode = "Format Error";
                    }
                }
                if (GUILayout.Button("Clear"))
                {
                    inputCode = "";
                }

                GUILayout.EndHorizontal();

                #region Presets
                GUILayout.Label("Presets");
                if (GUILayout.Button("Invert Current Bits"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (data[i] + 1) % 2;
                    }
                }
                if (GUILayout.Button("Blank"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = 0;
                    }
                }
                if (GUILayout.Button("Fill"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = 1;
                    }
                }
                if (GUILayout.Button("Checkerboard 1"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i % 2 == (i / 80 % 2)) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Checkerboard 2"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = ((i + (i / 80 % 2) * 2) % 4 >= 2) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Checkerboard 3"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = ((i + (i / 80 % 2) * 2 + 1) % 4 >= 2) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Checkerboard 4"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = ((i + (i / 80 % 2) * 4) % 8 >= 4) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Checkerboard 5"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = ((i + (i / 80 % 2) * 5) % 10 >= 5) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Checkerboard 6"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = ((i + (i / 80 % 2) * 10) % 20 >= 10) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Checkerboard 7"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = ((i + (i / 80 % 2) * 20) % 40 >= 20) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Checkerboard 8"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = ((i + (i / 80 % 2) * 40) % 80 >= 40) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Strip 1"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i % 2 == 0) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Strip 2"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i % 4 >= 2) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Strip 3"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i % 8 >= 4) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Strip 4"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i % 10 >= 5) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Strip 5"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i % 20 >= 10) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Strip 6"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i % 40 >= 20) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Strip 7"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i % 80 >= 40) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Ring"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 % 2 == 0) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Inverse Ring"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 % 2 == 1) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Cap"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 < 1) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Inverse Cap"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 < 1) ? 0 : 1;
                    }
                }
                if (GUILayout.Button("Skirt"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 > 2) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Inverse Skirt"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 > 2) ? 0 : 1;
                    }
                }
                if (GUILayout.Button("Belt"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 >= 1) && (i / 80 <= 2) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Inverse Belt"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 >= 1) && (i / 80 <= 2) ? 0 : 1;
                    }
                }
                if (GUILayout.Button("Top"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 < 2) ? 1 : 0;
                    }
                }
                if (GUILayout.Button("Bottom"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = (i / 80 < 2) ? 0 : 1;
                    }
                }
                if (GUILayout.Button("Random"))
                {
                    for (int i = 0; i < data.Length; i++)
                    {
                        data[i] = UnityEngine.Random.value < 0.5 ? 1 : 0;
                    }
                }
                #endregion

                GUILayout.Space(10);
                GUILayout.Label("Set Individual Bits");

                for (int i = 0; i < data.Length; i++)
                {
                    if (i % 80 == 0)
                    {
                        GUILayout.Label("==== Ring " + (Mathf.Floor(i / 80) + 1) + " ====");
                    }
                    data[i] = GUILayout.Toggle(data[i] > 0f, "  Bit #" + (i + 1)) ? 1f : 0f;
                }
            }

            GUILayout.EndScrollView();

            #endregion

            #region Update Option

            GUILayout.FlexibleSpace();

            #region Model Picker

            flatModel = GUILayout.Toggle(flatModel, "Flat Preview Model");
            SetPreviewModelTransform();

            #endregion

            GUILayout.BeginHorizontal();

            autoUpdate = GUILayout.Toggle(autoUpdate, "Auto Update");

            if (GUILayout.Button("Update"))
            {
                UpdateInfo();
            }

            GUILayout.EndHorizontal();

            #endregion

            if (autoUpdate)
            {
                UpdateInfo();
            }
        }

        private static Color HexToColor(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out Color c);
            return c;
        }

        private static string ColorToHex(Color n)
        {
            return "#" + ColorUtility.ToHtmlStringRGB(n);
        }

        #endregion

        #region Helper Methods
        private List<Transform> GetAllChildren(Transform t)
        {
            List<Transform> ts = new List<Transform>();

            foreach (Transform child in t)
            {
                ts.Add(child);
                ts.AddRange(GetAllChildren(child));
            }

            return ts;
        }

        private void SplitToArray(string binarystring)
        {
            if (binarystring.Length == 320)
            {
                for (int i = 0; i < binarystring.Length; i++)
                {
                    data[i] = binarystring[i] == '0' ? 0 : 1;
                }
            }
            else
            {
                throw new System.FormatException();
            }
        }

        private string CombineToBinString()
        {
            StringBuilder sb = new StringBuilder(data.Length);
            for (int i = 0; i < data.Length; i++)
                sb.Append(data[i] < 0.5 ? 0 : 1);
            string bin = sb.ToString();
            return bin;
        }

        private string HexToBin(string hex)
        {
            return string.Join(string.Empty, hex.Select(c => 
            System.Convert.ToString(System.Convert.ToInt32(c.ToString(), 16), 2)
                .PadLeft(4, '0')).ToArray());
        }

        private static string BinToHex(string bin)
        {
            return string.Join(" ", Enumerable.Range(0, bin.Length / 8).Select(i => 
            System.Convert.ToByte(bin.Substring(i * 8, 8), 2).ToString("X2"))
                .ToArray()).Replace(" ", "");
        }
        #endregion


        private void UpdateInfo()
        {
            ValidateParameter();

            bitMode = mode == 1;
            dataStore = BinToHex(CombineToBinString());

            MeshRenderer meshRenderer = previewModelTransform.GetComponent<MeshRenderer>();
            meshRenderer.sharedMaterial.SetFloatArray("data", bitMode ? data : Encode());
            meshRenderer.sharedMaterial.SetColor("_color_A", HexToColor(color1Store));
            meshRenderer.sharedMaterial.SetColor("_color_B", HexToColor(color2Store));
        }

        private void ValidateParameter()
        {
            FixLength(ref word_1, 8);
            FixLength(ref word_2, 8);
            FixLength(ref word_3, 8);
            FixLength(ref word_4, 8);
            FixLength(ref w1, 1);
            FixLength(ref w2, 1);

            c1 = Mathf.Clamp(c1, 0, 90);
            c2 = Mathf.Clamp(c2, 0, 59);
            c3 = Mathf.Clamp(c3, 0, 59);
            c4 = Mathf.Clamp(c4, 0, 180);
            c5 = Mathf.Clamp(c5, 0, 59);
            c6 = Mathf.Clamp(c6, 0, 59);
            w1 = w1[0].ToString();
            w2 = w2[0].ToString();
        }
    }
}
