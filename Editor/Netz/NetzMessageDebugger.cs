#if UNITY_COLLECTIONS && UNITY_TRANSPORT

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using NoZ.Netz;
using Unity.Networking.Transport;
using Unity.Networking.Transport.Utilities;
using Unity.Collections;
using UnityEngine.Assertions;
using UnityEditor.IMGUI.Controls;
using System;

namespace NoZ
{
    public class NetzMessageDebugger : EditorWindow
    {
#if false
        private const int MaxMessageCount = 4096;

        [Serializable]
        private struct Message
        {
            public uint from;
            public uint to;
            public FourCC id;
            public long time;
            public bool received;
            public int index;
            public int clientId;
            public int networkObjectId;
            public int length;
        }

        private class DebugClient
        {
            public NetworkConnection connection;
            public uint id;
            public bool connected;
        }

        private Message[] _messages = new Message[MaxMessageCount];
        private int _firstMessage = 0;
        private int _messageCount = 0;
        private Vector2 _scroll;
        private int _selectedClient = -1;
        private bool _clearOnPlay = true;

        // Add menu named "My Window" to the Window menu
        [MenuItem("Window/Analysis/NetZ Message Debugger")]
        static void Init()
        {
            var window = GetWindow<NetzMessageDebugger>();
            window.titleContent = new GUIContent("Netz Debugger");
            window.Show();
        }

        private void OnGUI()
        {
            Constants.Init();

            // After compilation and some other events data of the window is lost if it's not saved in some kind of container. Usually those containers are ScriptableObject(s).
            if (_multiColumnHeader == null)
                InitializeColumns();

            DrawToolbar();

            GUILayout.FlexibleSpace();

            // Get automatically aligned rect for our multi column header component.
            Rect windowRect = GUILayoutUtility.GetLastRect();
            windowRect.y += 2;

            // Here we are basically assigning the size of window to our newly positioned `windowRect`.
            windowRect.width = this.position.width;
            //windowRect.height = this.position.height;

            // This is a rect for our multi column table.
            var headerRect = new Rect(source: windowRect) { height = EditorGUIUtility.singleLineHeight };

            // Draw header for columns here.
            float xScroll = 0.0f;
            _multiColumnHeader.OnGUI(rect: headerRect, xScroll: xScroll);

            var scrollRect = windowRect;
            scrollRect.y += headerRect.height;
            scrollRect.height -= headerRect.height;

            var viewRect = windowRect;
            viewRect.y = 0;
            viewRect.height = headerRect.height * _messageCount;
            viewRect.width = MaxColumnWidth();
            _scroll = GUI.BeginScrollView(scrollRect, _scroll, viewRect);

            var rowRect = new Rect(source: headerRect) { y = 0 };

            for (int messageIndex = _firstMessage, messageCount = _messageCount; 
                messageCount > 0; 
                messageIndex = (messageIndex + 1) % MaxMessageCount, messageCount--, rowRect.y += headerRect.height)
            {
                if (rowRect.y + rowRect.height < _scroll.y)
                    continue;

                if (rowRect.y > _scroll.y + scrollRect.height)
                    break;

                ref var message = ref _messages[messageIndex];

                if (_selectedClient != -1 && message.from != (uint)_selectedClient)
                {
                    rowRect.y -= headerRect.height;
                    continue;
                }

                ColumnText(0, rowRect, new DateTime(message.time).ToString("hh:mm:ss.fff"));
                ColumnText(1, rowRect, message.from == 0 ? "-" : message.from.ToString());
                ColumnText(2, rowRect, message.to == 0 ? "-" : message.to.ToString());
                ColumnText(3, rowRect, message.received ? "RECV" : "SEND");
                ColumnText(4, rowRect, message.id.ToString());
                ColumnText(5, rowRect, message.length.ToString());
            }

            GUI.EndScrollView();
        }

        private float MaxColumnWidth ()
        {
            float max = 0.0f;
            for(int columnIndex =0; columnIndex <_columns.Length; columnIndex ++)
            {
                var visibleColumnIndex = _multiColumnHeader.GetVisibleColumnIndex(columnIndex: columnIndex);
                var columnRect = _multiColumnHeader.GetColumnRect(visibleColumnIndex: visibleColumnIndex);
                max = Mathf.Max(max, columnRect.xMax);
            }

            return max;
        }

        private void ColumnText (int columnIndex, Rect rowRect, string text)
        {
            if (!_multiColumnHeader.IsColumnVisible(columnIndex: columnIndex))
                return;

            var visibleColumnIndex = _multiColumnHeader.GetVisibleColumnIndex(columnIndex: columnIndex);
            var columnRect = _multiColumnHeader.GetColumnRect(visibleColumnIndex: visibleColumnIndex);

            columnRect.y = rowRect.y;
            columnRect.height = rowRect.height;

            GUIStyle nameFieldGUIStyle = new GUIStyle(GUI.skin.label)
            {
                padding = new RectOffset(left: 10, right: 10, top: 2, bottom: 2)
            };

            EditorGUI.LabelField(
                position: this._multiColumnHeader.GetCellRect(visibleColumnIndex: visibleColumnIndex, columnRect),
                label: new GUIContent(text),
                style: nameFieldGUIStyle
                );
        }


        private NetworkDriver _driver;
        private NetworkPipeline _pipeline;
        private List<DebugClient> _clients = new List<DebugClient>(16);

        private void OnEnable()
        {
            var endpoint = NetworkEndPoint.AnyIpv4;
            endpoint.Port = 9191;
            StartServer(endpoint);

            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (_clearOnPlay && state == PlayModeStateChange.EnteredPlayMode)
                Clear();
        }

        private void StartServer (NetworkEndPoint endpoint)
        {
            _driver = NetworkDriver.Create(new ReliableUtility.Parameters { WindowSize = 32 });
            _pipeline = _driver.CreatePipeline(typeof(ReliableSequencedPipelineStage));

            if (_driver.Bind(endpoint) != 0)
            {
                _driver.Dispose();
                return;
            }
            else
                _driver.Listen();
        }

        private void Update()
        {
            if (!_driver.IsCreated)
                return;

            _driver.ScheduleUpdate().Complete();

            // Clean up any disconnected clients
            for (int i = 0; i < _clients.Count; i++)
            {
                if (!_clients[i].connection.IsCreated)
                {
                    _clients.RemoveAtSwapBack(i);
                    --i;
                }
            }

            // Accept new connections
            NetworkConnection c;
            while ((c = _driver.Accept()) != default(NetworkConnection))
            {
                var client = new DebugClient { connection = c };
                _clients.Add(client);
            }

            // Read incoming data from all clients
            DataStreamReader stream;
            for (int i = 0; i < _clients.Count; i++)
            {
                var client = _clients[i];
                Assert.IsTrue(client.connection.IsCreated);

                NetworkEvent.Type cmd;
                while ((cmd = _driver.PopEventForConnection(client.connection, out stream)) != NetworkEvent.Type.Empty)
                {
                    if (cmd == NetworkEvent.Type.Data)
                    {
                        ReadMessage(client, ref stream);
                    }
                    else if (cmd == NetworkEvent.Type.Disconnect)
                    {
                        _clients.RemoveAtSwapBack(i);
                        i--;
                    }
                }
            }
        }

        private void ReadMessage (DebugClient client, ref DataStreamReader reader)
        {
            // Read the message FourCC
            var messageId = new FourCC(reader.ReadUInt());

            if(messageId == NetzConstants.Messages.Connect)
            {
                client.id = reader.ReadUInt();
                client.connected = true;
                Repaint();
                return;
            }

            if (messageId != NetzConstants.Messages.Debug)
                return;

            var count = (int)reader.ReadUShort();
            for (int i = 0; i < count; i++)
            {
                var message = new Message
                {
                    from = reader.ReadUInt(),
                    to = reader.ReadUInt(),
                    id = reader.ReadFourCC(),
                    received = reader.ReadByte() == 1,
                    length = reader.ReadUShort(),
                    time = DateTime.Now.Ticks
                };

                if (_messageCount == MaxMessageCount)
                {
                    _messages[_firstMessage] = message;
                    _firstMessage = (_firstMessage + 1) % MaxMessageCount;
                }
                else
                {
                    var messageIndex = (_firstMessage + _messageCount) % MaxMessageCount;
                    if (messageIndex < 0 || messageIndex >= _messages.Length)
                        Debug.Log("hmm");
                    _messages[messageIndex] = message;
                    _messageCount++;
                }

                // Scroll down a line for the new entry
                _scroll.y += EditorGUIUtility.singleLineHeight;
                _scroll.y = Mathf.Min(_scroll.y, (_messageCount - 1) * EditorGUIUtility.singleLineHeight);
            }

            Repaint();
        }

        private void OnDisable()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;

            foreach (var client in _clients)
                if (client.connection.IsCreated)
                    client.connection.Close(_driver);

            _clients.Clear();

            if(_driver.IsCreated)
                _driver.Dispose();
        }

        private MultiColumnHeaderState.Column[] _columns;
        private MultiColumnHeaderState _multiColumnHeaderState;
        private MultiColumnHeader _multiColumnHeader;

        private void InitializeColumns ()
        {
            _columns = new MultiColumnHeaderState.Column[] {
                new MultiColumnHeaderState.Column()
                {
                    autoResize = false,
                    allowToggleVisibility = true,
                    minWidth = 100.0f,
                    width = 100.0f,
                    headerContent = new GUIContent("Time", "Time the message was received"),
                    headerTextAlignment = TextAlignment.Left,
                },
                new MultiColumnHeaderState.Column()
                {
                    autoResize = false,
                    allowToggleVisibility = false,
                    minWidth = 40,
                    width = 40,
                    headerContent = new GUIContent("From", "Client or server the message is from"),
                    headerTextAlignment = TextAlignment.Left,
                },
                new MultiColumnHeaderState.Column()
                {
                    autoResize = false,
                    allowToggleVisibility = false,
                    minWidth = 40,
                    width = 40,
                    headerContent = new GUIContent("To", "Client the message is from or two"),
                    headerTextAlignment = TextAlignment.Left,
                },
                new MultiColumnHeaderState.Column()
                {
                    autoResize = false,
                    allowToggleVisibility = false,
                    minWidth = 55,
                    width = 55,
                    headerContent = new GUIContent("Flow", "Send or receive"),
                    headerTextAlignment = TextAlignment.Left,
                },
                new MultiColumnHeaderState.Column()
                {
                    autoResize = false,
                    allowToggleVisibility = false,
                    minWidth = 60,
                    width = 60,
                    headerContent = new GUIContent("ID", "Message Identifier"),
                    headerTextAlignment = TextAlignment.Left,
                },
                new MultiColumnHeaderState.Column()
                {
                    autoResize = false,
                    allowToggleVisibility = false,
                    minWidth = 60.0f,
                    width = 60.0f,
                    headerContent = new GUIContent("Length", "Message Length"),
                    headerTextAlignment = TextAlignment.Left,
                }
            };

            _multiColumnHeaderState = new MultiColumnHeaderState(columns: this._columns);

            _multiColumnHeader = new MultiColumnHeader(state: this._multiColumnHeaderState);

            // When we chagne visibility of the column we resize columns to fit in the window.
            _multiColumnHeader.visibleColumnsChanged += (multiColumnHeader) => multiColumnHeader.ResizeToFit();

            // Initial resizing of the content.
            _multiColumnHeader.ResizeToFit();
        }

        private class Constants
        {
            private static bool _loaded;
            public static GUIStyle Toolbar;

            public const int kFontSize = 10;
            public const int kFixedHeight = kFontSize + 9;

            public static readonly GUIContent Clear = EditorGUIUtility.TrTextContent("Clear", "Clear messages");
            public static GUIStyle ToolbarPopup = new GUIStyle("toolbarPopup"); //  { fontSize = kFontSize, fixedHeight = kFixedHeight };

            public static void Init()
            {
                if (_loaded)
                    return;

                _loaded = true;
                
                Toolbar = "Toolbar";
            }
        }

        private void DrawToolbar ()
        {
            GUILayout.BeginHorizontal(Constants.Toolbar, GUILayout.Height(EditorStyles.toolbar.fixedHeight), GUILayout.ExpandWidth(true));

            GUILayout.Label(_driver.IsCreated ? $"Online ({_clients.Count})" : "Offline", EditorStyles.toolbarButton, GUILayout.Width(80));

            DrawClients();

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
            {
                Clear();
            }

            _clearOnPlay = GUILayout.Toggle(_clearOnPlay, "Clear On Play", EditorStyles.toolbarButton);

            GUILayout.EndHorizontal();
        }

        private bool CheckClientEnabled (int index)
        {
#if false
            // Enable items like <Enter IP>
            var devices = m_Runtime.DeviceQuery.Devices;
            if (index >= devices.Count)
                return true;
            return devices.Values.ToArray()[index].State == IAndroidLogcatDevice.DeviceState.Connected;
#else
            return true;
#endif
        }

        private void ClientSelection (object userData, string[] options, int selected)
        {
#if false
            var devices = m_Runtime.DeviceQuery.Devices;
            if (selected >= m_Runtime.DeviceQuery.Devices.Count)
            {
                AndroidLogcatIPWindow.Show(this.m_Runtime, m_IpWindowScreenRect);
                return;
            }

            SelectedPackage = null;
            m_Runtime.DeviceQuery.SelectDevice(devices.Values.ToArray()[selected]);
#endif
            var option = options[selected];
            if (option == "All")
                _selectedClient = -1;
            else if (option == "Server")
                _selectedClient = 0;
            else
                _selectedClient = int.Parse(options[selected].Substring(7));
        }


        private void DrawClients ()
        {
//            var selectedDevice = m_Runtime.DeviceQuery.SelectedDevice;
//            var currentSelectedDevice = selectedDevice == null ? "No device" : selectedDevice.DisplayName;
            GUILayout.Label(new GUIContent("All", "Select android device"), Constants.ToolbarPopup, GUILayout.Width(100));

            var rect = GUILayoutUtility.GetLastRect();
            if (Event.current.type == EventType.MouseDown && rect.Contains(Event.current.mousePosition))
            {
                List<GUIContent> names = new List<GUIContent>() { new GUIContent("All") };

                var selectedIndex = 0;
                for(int clientIndex=0; clientIndex< _clients.Count; clientIndex++)
                {
                    var client = _clients[clientIndex];
                    if (client.connected)
                    {
                        names.Add(new GUIContent(client.id == 0 ? "Server" : $"Client {client.id}"));

                        if (client.id == _selectedClient)
                            selectedIndex = clientIndex + 1;
                    }
                }

                EditorUtility.DisplayCustomMenu(new Rect(rect.x, rect.yMax, 0, 0), names.ToArray(), CheckClientEnabled, selectedIndex, ClientSelection, null);
            }
        }

        private void Clear()
        {
            _firstMessage = 0;
            _messageCount = 0;
            Repaint();
        }
#endif
    }
}

#endif