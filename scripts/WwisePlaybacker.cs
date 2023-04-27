using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Timers;
using Unity.Plastic.Newtonsoft.Json;
using Unity.Plastic.Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEditor.Recorder;
using UnityEditor.Recorder.Input;

using Timer = System.Timers.Timer;


[System.Serializable]
public class LogWwiseEventItem 
{
    public int time;
    public List<string> names;

}
public class WwisePlaybacker : EditorWindow
{


    enum PlayBackeState
    {
        等待录制或加载,
        录制中,
        完成录制,
        等待播放,
        播放中,
        已暂停,
        恢复播放,
        停止播放,
    }

    PlayBackeState playBackerState = PlayBackeState.等待录制或加载;

    private static List<string> logItemJsonList = new List<string>(); //日志条目json收集缓存列表
    static List<LogWwiseEventItem> ItemListForPlay = new List<LogWwiseEventItem>();
   

    [MenuItem("GAMI/Tools/音频日志录制回放器")]
    public static void ShowWindow()
    {
        WwisePlaybacker wwisePlaybacker = GetWindow<WwisePlaybacker>();
        wwisePlaybacker.Show();
    }


    private async void Awake()
    {
        ResetPlayButton();
        ResetToBeginPlay();
        ResetToStartWwiseEventCapture();
        await WaapiManager.Connect();
        Initialize();
    }

    RecorderController m_RecorderController;
    public bool m_RecordAudio = true;
    internal MovieRecorderSettings m_Settings = null;
    public FileInfo OutputFile
    {
        get
        {
            var fileName = m_Settings.OutputFile + ".mp4";
            return new FileInfo(fileName);
        }
    }
    internal void Initialize()
    {
        var controllerSettings = ScriptableObject.CreateInstance<RecorderControllerSettings>();
        m_RecorderController = new RecorderController(controllerSettings);

        var mediaOutputFolder = new DirectoryInfo(Path.Combine(Application.dataPath, "..", "SampleRecordings"));

        // Video
        m_Settings = ScriptableObject.CreateInstance<MovieRecorderSettings>();
        m_Settings.name = "My Video Recorder";
        m_Settings.Enabled = true;

        // This example performs an MP4 recording
        m_Settings.OutputFormat = MovieRecorderSettings.VideoRecorderOutputFormat.MP4;
        m_Settings.VideoBitRateMode = VideoBitrateMode.Low;

        m_Settings.ImageInputSettings = new GameViewInputSettings
        {
            OutputWidth = 600,
            OutputHeight = 800
        };

        m_Settings.AudioInputSettings.PreserveAudio = m_RecordAudio;

        // Simple file name (no wildcards) so that FileInfo constructor works in OutputFile getter.
        m_Settings.OutputFile = mediaOutputFolder.FullName + "/" + "video";

        // Setup Recording
        controllerSettings.AddRecorderSettings(m_Settings);
        controllerSettings.SetRecordModeToManual();
        controllerSettings.FrameRate = 30.0f;

        RecorderOptions.VerboseMode = false;
        //m_RecorderController.PrepareRecording();
        //m_RecorderController.StartRecording();
        Debug.Log("========视频开始录制");
        Debug.Log($"Started recording for file {OutputFile.FullName}");
    }
    private void OnDestroy()
    {
        if (timer != null) { timer.Dispose(); }
        WaapiManager.ClearAllRegistedGameobjectIds();
        // WaapiManager.ClearUpdateDelegate();
        m_RecorderController.StopRecording();
    }

    bool isStartRecording = false;
    Color recordColor = Color.white;

    bool isPlay = false;
    string playStopButtonIcon = "d_PlayButton";
    Color playButtonColor = Color.white;

    bool isPause = false;
    string pauseResumeIcon = "PauseButton";
    Color pauseButtonColor = Color.white;

    private void UpdateButtonStyle() 
    {       
        recordColor = isStartRecording ? Color.red : Color.white;
        playStopButtonIcon = isPlay ? "d_preAudioPlayOn" : "d_PlayButton";
        playButtonColor = isPlay ? Color.red : Color.white;
        pauseResumeIcon = isPause ? "PauseButton On" : "PauseButton";
        pauseButtonColor = isPause ? Color.red : Color.white;
    }


    private void OnGUI()
    {
        UpdateButtonStyle();

        GUI.contentColor = Color.green;
        EditorGUILayout.LabelField(new GUIContent(playBackerState.ToString()), EditorStyles.centeredGreyMiniLabel, GUILayout.Height(66f));
        GUI.contentColor = Color.white;

        GUI.backgroundColor = recordColor;

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(EditorGUIUtility.IconContent("d_Animation.Record")))
        {
            isStartRecording = !isStartRecording;

            {
                if (!isStartRecording)
                    StopWwiseEventCapture();
                else
                    StartWwiseEventCapture();
            }

            playBackerState = PlayBackeState.录制中;

        }

        GUI.backgroundColor = playButtonColor;

        if (GUILayout.Button(EditorGUIUtility.IconContent(playStopButtonIcon)))
        {
            if (ItemListForPlay.Count > 0)
            {
                isPlay = !isPlay;

                {
                    if (isPlay)
                        Play();
                    else
                        Stop();
                }

                playBackerState = isPlay ? PlayBackeState.播放中 : PlayBackeState.停止播放;
            }

        }
       
        GUI.backgroundColor = pauseButtonColor;

        if (GUILayout.Button(EditorGUIUtility.IconContent(pauseResumeIcon)))
        {
            if (isPlay)
            {
                isPause = !isPause;

                {
                    if (isPause)
                        Pause();
                    else
                        Resume();
                }

                playBackerState = isPause ? PlayBackeState.已暂停 : PlayBackeState.恢复播放;
            }

        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        //保存
        if (GUI.Button(new Rect(this.position.width * 0.25f, this.position.height * 0.75f, 
            this.position.width / 4, this.position.height / 10),new GUIContent("Save")))
        {
            SaveCaptureDataToJson();
        }
        //加载
        if (GUI.Button(new Rect(this.position.width * 0.5f, this.position.height * 0.75f,
            this.position.width / 4, this.position.height / 10), new GUIContent("Load")))
        {
            if (LoadCaptureDataFromJson())
            {
                playBackerState = PlayBackeState.等待播放;
            }
            GAMI.AudioManager.Instance.LoadBank("MyBank");

        }


        EditorGUILayout.EndHorizontal();

    }

    private void OnInspectorUpdate()
    {
        this.Repaint();
    }


    public int lastTime = 0;
    public uint subscribeId = 0;

    public Timer timer;

    public void StartWwiseEventCapture()
    { 
        m_RecorderController.PrepareRecording();
        m_RecorderController.StartRecording();
        // if (suscribeId > 0) { return; }//避免重复订阅

        ResetToStartWwiseEventCapture();
        Debug.Log("========Json开始录制");
        GAMI.AudioManager.Instance.PlaySound2D("Play_sine");
        WaapiManager.OnCaptureLogItemAdded(new string[] { Profiler_Log_Item_Type.Event },
            (string json) =>
            {
                Debug.Log(json);
                logItemJsonList.Add(json);
            },
            (uint id) =>
            {
                subscribeId = id;
            }
            );
    }


    public void StopWwiseEventCapture()
    {
        WaapiManager.Unsubscribe(subscribeId);//取消订阅
        Debug.Log("========Json停止录制");
        ItemListForPlay = GenerateLogItemListFromJsonList(logItemJsonList);
        BuildEventRelativeTime(ItemListForPlay);
        m_RecorderController.StopRecording();
        Debug.Log("========视频停止录制");
    }

    int nowTime = 0;
    int index = 0;


    public void CallBack(object sender, ElapsedEventArgs e) 
    {
        if (index >= ItemListForPlay.Count)
        {
            timer.Close();
            ResetPlayButton();  
            ResetToBeginPlay();
            WaapiManager.UnRegisterGameObject(1);
            playBackerState = PlayBackeState.等待播放;
            return;
        }

        nowTime = (int)(nowTime + timer.Interval);
        LogWwiseEventItem item = ItemListForPlay[index];
        int itemTime = item.time;
        List<string> itemNames = item.names;
        if (itemTime == nowTime)
        {
            for (int i = 0; i < itemNames.Count; i++)
            {           
                WaapiManager.PostEventByName(itemNames[i], 1);
            }
            index++;
        }
    }

    public void Play()
    {
        if (ItemListForPlay.Count <= 0) 
        {
            this.ShowNotification(new GUIContent("没有可供回放的音频事件"), 6);
            ResetPlayButton();
            return;      
        }

        ResetToBeginPlay();
        WaapiManager.RegisterGameObject(1, "zcg");

        if (timer == null) { timer = new Timer(1); }

        timer.Elapsed -= CallBack;

        timer.Elapsed += CallBack;
        timer.Start();
    }

    public void Pause() 
    {
        if (timer != null) { timer.Stop(); }           
    }


    public void Resume() 
    {
        if (timer != null) { timer.Start(); }
    }

    public void Stop() 
    {
        if (timer != null)
        {
            timer.Stop();
            WaapiManager.UnRegisterGameObject(1);
        }

    }

    /// <summary>
    /// 触发时间计算到以0.5s为起始时间
    /// </summary>
    /// <param name="logItemList"></param>
    public void BuildEventRelativeTime(List<LogWwiseEventItem> logItemList) 
    {
        if (logItemList.Count >= 1)
        {
            int zeroTime = logItemList[0].time > 500 ? logItemList[0].time - 500 : logItemList[0].time;
            foreach (var item in logItemList)
            {
                item.time -= zeroTime;
            }
        }

    }

    public List<LogWwiseEventItem> GenerateLogItemListFromJsonList(List<string> resultList)
    {
        List<LogWwiseEventItem> logItemList = new List<LogWwiseEventItem>();
        
        for (int i = 0; i < resultList.Count; i++) 
        {
            WaapiManager.LogItem item = JsonUtility.FromJson<WaapiManager.LogItem>(resultList[i]);
            int logItemtime = item.time;
            string LogObjName = item.objectName;

            if (logItemtime > lastTime)
            {
                LogWwiseEventItem logWwiseEventItem = new LogWwiseEventItem();
                logWwiseEventItem.time = logItemtime;
                logWwiseEventItem.names = new List<string>();
                logWwiseEventItem.names.Add(LogObjName);

                logItemList.Add(logWwiseEventItem);
                lastTime = logItemtime;
            }
            else if (logItemtime == lastTime)
            {
                LogWwiseEventItem lastItem = logItemList.FindLast((LogWwiseEventItem lastIt) => { return lastIt.time == lastTime; });
                lastItem.names.Add(LogObjName);
            }
            EditorUtility.DisplayProgressBar("wait", "生成中……", i / resultList.Count);

        }
        EditorUtility.ClearProgressBar();
        return logItemList;
    }


    public void ResetToBeginPlay() 
    {
        nowTime = 0;
        index = 0;
        
    }

    public void ResetToStartWwiseEventCapture() 
    {
        logItemJsonList.Clear();
        ItemListForPlay.Clear();
        lastTime = 0;
        subscribeId = 0;
    
    }

    public void ResetPlayButton() 
    {
        playButtonColor = Color.white;
        isPlay = false;
    }


    public void SaveCaptureDataToJson() 
    {
        string filePath = EditorUtility.SaveFilePanel("保存", Application.dataPath, SoundToolsUtility.GetNowTimeToString(), "playbacker");
        if (string.IsNullOrEmpty(filePath)) { return; }
        string resJson = SoundToolsUtility.ConvertListToJson<LogWwiseEventItem>(ItemListForPlay);
        File.WriteAllText(filePath, resJson, System.Text.Encoding.UTF8);
    }

    public bool LoadCaptureDataFromJson() 
    {
        string filePath = EditorUtility.OpenFilePanelWithFilters("加载", Application.dataPath, new string[] { "Json","playbacker" });
        if (string.IsNullOrEmpty(filePath)) { return false; }

        if (File.Exists(filePath))
        {
            string jsData = File.ReadAllText(filePath, System.Text.Encoding.UTF8);
            JObject jo = JObject.Parse(jsData);
            ItemListForPlay.Clear();
            ItemListForPlay = jo["items"].ToObject<List<LogWwiseEventItem>>();

            if (ItemListForPlay.Count > 0) { return true; }

        }
        return false;
    
    }



}
