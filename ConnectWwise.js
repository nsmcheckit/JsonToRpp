// 引入依赖库
const Client = require('node-rest-client').Client;
const ak = require('waapi').ak;

// 设置HTTP参数
const httpParams = {
    data: {
        uri: ak.wwise.core.getInfo,
        options: {},
        args: {},
    },
    headers: { 'Content-Type': 'application/json' },
};
// 创建客户端
const client = new Client();

function helloWwise() {
    // 发送请求
    const req = client.post('http://localhost:8090/waapi', httpParams, function (objectPayload, response) {
        if (response.statusCode !== 200) {
            if (response.headers['content-type'] === 'application/json') {
                console.log(`Error: ${objectPayload.uri}: ${JSON.stringify(objectPayload)}`);
            } else {
                console.log(`Error: ${(new Buffer.from(objectPayload)).toString('utf8')}`);
            }
            return;
        }

        console.log(`Hello ${objectPayload.displayName} ${objectPayload.version.displayName}`);
    });

    // 处理请求错误
    req.on('error', function (err) {
        console.log(`Error: ${err.message}`);
    });

};

async function getEventTypeAndTargetLength(eventName) {
    // 查询事件对象、事件类型、目标和音频长度
    const queryResult = await new Promise((resolve, reject) => {
        const queryParams = {
            data: {
                uri: ak.wwise.core.object.get,
                options: { return: ['name', 'audioSource:playbackDuration', 'path'] },
                args: { waql: `$ "/Events" select descendants where name ="${eventName}"` }
            },
            headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
        };

        client.post('http://localhost:8090/waapi', queryParams, function (queryResult, response) {
            //console.log('Request:', JSON.stringify(queryParams, null, 2));
            //console.log('Response:', JSON.stringify(data, null, 2));
            if (response.statusCode !== 200) {
                console.error(`查询事件失败: ${eventName}，状态码：${response.statusCode}，错误消息：${data.message}`);
                reject(new Error(`查询事件失败: ${eventName}`));
                return;
            }

            resolve(queryResult);
        });
    });
    if (!queryResult) {
        console.log(`找不到事件: ${eventName}`);
        return null;
    }

    return resolveWaapiResposeData(queryResult);
}

function resolveWaapiResposeData(queryResult) {
    if (queryResult.return && queryResult.return.length > 0) {
        return queryResult.return[0]["audioSource:playbackDuration"]["playbackDurationMax"];
        // switch (data.return[0]["audioSource:playbackDuration"]["playbackDurationType"]) {
        //     case 'oneShot': return data.return[0]["audioSource:playbackDuration"]["playbackDurationMax"];
        //     case 'mixed': return data.return[0]["audioSource:playbackDuration"]["playbackDurationMax"];//random container
        // }
    }
    return null;
}

module.exports = {
    Client,
    ak,
    httpParams,
    client,
    helloWwise,
    getEventTypeAndTargetLength,
    resolveWaapiResposeData,
};

getEventTypeAndTargetLength("Play_Skill01")