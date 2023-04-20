const { client, Client, ak, initWaapi, helloWwise, getEventTypeAndTargetLength, resolveWaapiResposeData } = require('./ConnectWwise.js');
require('./ConnectWwise.js');
const fs = require('fs');
const rppp = require('rppp');
const wavFileInfo = require("wav-file-info");
const util = require('util');
const readFileAsync = util.promisify(fs.readFile);
const writeFileAsync = util.promisify(fs.writeFile);

//wav文件夹路径
const wavFoldPath = "./video.mp4";

// 输入 JSON 文件路径
const inputJsonPath = './input.json';

// 输出 RPP 文件路径
const outputRppPath = './output.rpp';

// 从 JSON 文件中读取数据
async function processJsonFile() {
    try {
        const jsonString = await readFileAsync(inputJsonPath, 'utf-8');
        const jsonData = JSON.parse(jsonString);
        const rppProject = await createRppProject(jsonData);
        await writeFileAsync(outputRppPath, rppProject.dump());
        console.log('RPP file created:', outputRppPath);
    } catch (err) {
        if (err.code === 'ENOENT') {
            console.error('Error reading file:', err);
        } else if (err instanceof SyntaxError) {
            console.error('Error parsing JSON:', err);
        } else {
            console.error('Error writing file:', err);
        }
    }
}

// 根据输入的 JSON 数据创建 RPP 工程
async function createRppProject(jsonData) {
    const project = new rppp.objects.ReaperProject(); // 新建一个rpp项目

    // 默认工程属性
    project.bpm = 120;
    project.timeSignature = [4, 4];
    let duration;
    // 为每个项目创建轨道
    for (const [index, itemData] of jsonData.items.entries()) {
        const track = new rppp.objects.ReaperTrack(); // 新建一个track
        track.name = itemData.names[0];
        duration = parseFloat(await getEventDuration(track.name));
        const item = new rppp.objects.ReaperItem(); // 新建一个item
        item.add({ token: "POSITION", params: [itemData.time / 1000] });
        item.add({ token: "LENGTH", params: [duration] });
        item.add({ token: "NAME", params: [track.name] });
        const source = new rppp.objects.ReaperSource();
        source.add({
            token: "",
            params: [],
            //params: [wavFoldPath + itemData.names[0] + ".wav"]
        });
        item.add(source);

        track.add(item);
        project.addTrack(track);
        project.getOrCreateStructByToken("TRACK", index).add({
            token: "NAME",
            params: [track.name],
        });
    }
    //视频
    const videoTrack = new rppp.objects.ReaperTrack(); // 新建一个视频轨
    videoTrack.name = "Video";
    const videoItem = new rppp.objects.ReaperItem(); // 新建一个视频item
    videoItem.add({ token: "POSITION", params: [0] });
    videoItem.add({ token: "LENGTH", params: [10000] });
    videoItem.add({ token: "NAME", params: ["Video"] });
    videoItem.getOrCreateStructByToken("<SOURCE VIDEO", 0);
    videoItem.getOrCreateStructByToken(">", 0);

    // videoTrack.contents.push({
    //     "token": "SOURCE VIDEO",
    //     "params": [],
    // });
    //const videoSource = new rppp.objects.ReaperSource();
    // videoItem.add({
    //     token: "SOURCE VIDEO",
    //     //params: [{token: "FILE", params: ["video.mp4"]}] //视频文件路径
    // });
    //videoItem.add(videoSource);
    videoTrack.add(videoItem);
    project.addTrack(videoTrack);
    console.log(project.contents);

    return project;
}


async function getEventDuration(eventName) {
    var eventDuration = await getEventTypeAndTargetLength(eventName);
    if (eventDuration == null || eventDuration == 0) {
        console.log("获取事件时长失败");
        return 0;
    }
    eventDuration = parseFloat(eventDuration, 16);
    //console.log(eventDuration)
    return eventDuration;
    //if(!eventDuration == null && eventDuration != 0){
    //return eventDuration;
    //}
    // else{
    //     console.log("获取事件时长失败");
    //     return 0;
    // }
}

processJsonFile()