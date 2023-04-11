const fs = require('fs');
const rppp = require('rppp');
const wavFileInfo = require("wav-file-info");

//wav文件夹路径
const wavFoldPath = "F:\\JsonToRppp\\" ;

// 输入 JSON 文件路径
const inputJsonPath = './input.json';

// 输出 RPP 文件路径
const outputRppPath = './output.rpp';

// 从 JSON 文件中读取数据
fs.readFile(inputJsonPath, 'utf-8', (err, jsonString) => {
    if (err) {
        console.error('Error reading file:', err);
        return;
    }

    try {
        // 解析 JSON 数据
        const jsonData = JSON.parse(jsonString);

        // 创建 RPP 工程
        const rppProject = createRppProject(jsonData);

        // 将 RPP 工程写入文件
        fs.writeFile(outputRppPath, rppProject.dump(), (err) => {
            if (err) {
                console.error('Error writing file:', err);
            } else {
                console.log('RPP file created:', outputRppPath);
            }
        });
    } catch (err) {
        console.error('Error parsing JSON:', err);
    }
});

// 根据输入的 JSON 数据创建 RPP 工程
function createRppProject(jsonData) {
    const project = new rppp.objects.ReaperProject();//新建一个rpp项目

    // 默认工程属性
    project.bpm = 120;
    project.timeSignature = [4, 4];

    // 为每个项目创建轨道
    jsonData.items.forEach((itemData, index) => {
        const track = new rppp.objects.ReaperTrack();//新建一个track
        track.name = itemData.names[0];

        const item = new rppp.objects.ReaperItem();//新建一个item
        item.add({ token: "POSITION", params: [itemData.time/1000] });
        item.add({ token: "LENGTH", params: [3000] });
        
        const source = new rppp.objects.ReaperSource();
         source.add({ 
            token: "FILE", 
            params: [ wavFoldPath + itemData.names[0] + ".wav" ] 
            });
        item.add(source);


        track.add(item);
        project.addTrack(track);
        project.getOrCreateStructByToken("TRACK", index).add({
            token: "NAME",
            params: [track.name],
        });
    });

    return project;
}

function getWavDuartion(wavPath){
    const wavInfo = wavFileInfo.getInfoByFilenameSync(wavPath);
    console.log(wavInfo);
    return wavInfo.duration;
}