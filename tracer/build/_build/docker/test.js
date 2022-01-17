var http = require('http');
http.createServer(async (request, response) => {
    const options = {
        hostname: 'integrationtests',
        port: 5002,
        path: '/',
        method: 'GET'
    };
    const req2 = http.request(options, res => {
        console.log(`statusCode: ${res.statusCode}`)
        req2.end()
    });
    const buffers = [];
    for await (const chunk of request) {
        buffers.push(chunk);
    }
    const data = Buffer.concat(buffers).toString();
    console.log(data);
    try {
        console.log(JSON.parse(data));
    } catch (_) {
        console.log('nop');
    }
    response.writeHead(200, {
        'x-datadog-trace-id': '1111',
        'x-datadog-span-id': '2222'
    });
    response.end();
}).listen(9003);