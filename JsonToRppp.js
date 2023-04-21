const { client, Client, ak, initWaapi, helloWwise, getEventTypeAndTargetLength, resolveWaapiResposeData } = require('./ConnectWwise.js');
require('./ConnectWwise.js');
const fs = require('fs');
const path = require('path');
const rppp = require('rppp');
const util = require('util');
const readFileAsync = util.promisify(fs.readFile);
const writeFileAsync = util.promisify(fs.writeFile);

//项目路径
const dirPath = './';

//文件路径
const mp4Path = dirPath + findMp4Files(dirPath);

// 输入 JSON 文件路径
const inputJsonPath = dirPath + findPlaybackerFiles(dirPath);

// 输出 RPP 文件路径
const outputRppPath = './output.rpp';

// 判断是否为 Playbacker 文件
function isPlaybackerFile(filename) {
    const playbackerExtension = '.playbacker';
    return filename.endsWith(playbackerExtension);
}

// 判断是否为 MP4 文件
function isMp4(filename) {
    const mp4Extension = '.mp4';
    return filename.endsWith(mp4Extension);
}

// 递归查找文件夹中的所有 Playbacker 文件
function findPlaybackerFiles(dirPath) {
    const playbackerFiles = [];

    fs.readdirSync(dirPath).forEach((file) => {
        const fullPath = path.join(dirPath, file);

        if (fs.statSync(fullPath).isDirectory()) {
            playbackerFiles.push(...findPlaybackerFiles(fullPath));
        } else {
            if (isPlaybackerFile(file)) {
                playbackerFiles.push(fullPath);
            }
        }
    });

    return playbackerFiles;
}

// 递归查找文件夹中的所有 mp4 文件
function findMp4Files(dirPath) {
    const mp4Files = [];

    fs.readdirSync(dirPath).forEach((file) => {
        const fullPath = path.join(dirPath, file);

        if (fs.statSync(fullPath).isDirectory()) {
            mp4Files.push(...findMp4Files(fullPath));
        } else {
            if (isMp4(file)) {
                mp4Files.push(fullPath);
            }
        }
    });

    return mp4Files;
}

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
    let position;
    // 为每个项目创建轨道
    for (const [index, itemData] of jsonData.items.entries()) {
        const track = new rppp.objects.ReaperTrack(); // 新建一个track
        track.name = itemData.names[0];
        duration = parseFloat(await getEventDuration(track.name));
        position = itemData.time / 1000;
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
    videoTrack.add({ token: "NAME", params: ["Video"] });
    const videoItem = new rppp.objects.ReaperItem(); // 新建一个视频item
    videoItem.add({ token: "POSITION", params: [0] });
    videoItem.add({ token: "LENGTH", params: [position + duration] });
    videoItem.add({ token: "NAME", params: ["Video"] });
    videoItem.getOrCreateStructByToken("<SOURCE VIDEO", 0);
    videoItem.getOrCreateStructByToken("FILE video.mp4", 0);
    videoItem.getOrCreateStructByToken(">", 0);
    videoTrack.add(videoItem);
    project.addTrack(videoTrack);

    return project;
}


async function getEventDuration(eventName) {
    var eventDuration = await getEventTypeAndTargetLength(eventName);
    if (eventDuration == null || eventDuration == 0) {
        console.log("获取事件时长失败");
        return 0;
    }
    eventDuration = parseFloat(eventDuration, 16);

    return eventDuration;
}

processJsonFile()