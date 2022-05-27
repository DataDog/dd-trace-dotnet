FROM node:16-slim
WORKDIR /app
RUN echo "\
    var http = require('http');\
    http.createServer(async (request, response) => {\
    response.writeHead(200, {});\
    response.end();\
    }).listen(9003);\
    " > app.js
CMD ["app.js"]
