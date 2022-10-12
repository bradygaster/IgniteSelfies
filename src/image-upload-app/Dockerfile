FROM node:18-alpine AS build

# make the 'app' folder the current working directory
WORKDIR /app

COPY . .

# Fix up entrypoint line endings and make exec
RUN apk update && apk add --no-cache dos2unix \
    && apk del dos2unix

# install project dependencies
ENV AZURE_STORAGE_CONNECTION_STRING "UseDevelopmentStorage=true"
ENV HUB_HOST "http://localhost:3001"

RUN npm install

EXPOSE 3000

ENTRYPOINT ["npm", "start"]
