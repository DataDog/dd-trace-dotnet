FROM node:16-slim
WORKDIR /app
RUN echo "\
    var http = require('http');\
    http.createServer(async (request, response) => {\
    response.writeHead(200, {\
    'x-datadog-trace-id': '1234',\
    'x-datadog-sampling-priority': '1'\
    });\
    response.end();\
    }).listen(9004);\
    " > app.js
CMD ["app.js"]
