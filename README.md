
# Ignite demo

This sample demonstrates how to use the Azure Storage SDK in the context of an [Express](https://expressjs.com/) application to upload images into Azure Blob Storage. Then, the app sends a message via Dapr pub/sub to let a .NET app know an image has been uploaded. 

## Getting started

Clone the repository to your machine:

```bash
git clone https://github.com/bradygaster/ignite-2022-demo.git
```

## Add the storage connection string

Navigate to the [Azure Portal](https://portal.azure.com) and copy the connection string from your storage account (under **Settings** > **Access keys**) to the `src/image-upload-app/.env` file, and to the `src/image-upload-subscriber/appsettings.json`. 

## Start the image uploaded subscriber

```bash
cd src/image-upload-subscriber
dotnet restore
dotnet build
dapr run --app-port 3001 --app-id image-upload-subscriber --app-protocol http --dapr-http-port 3501 --components-path ../../components -- dotnet run
```

## Start the image upload server

```bash
cd src/image-upload-app
npm install
dapr run --app-port 3000 --app-id image-upload --app-protocol http --components-path ../../components -- npm run start
```

Navigate to [http://localhost:3000](http://localhost:3000) and upload an image to blob storage.

You can use the [Azure Storage Explorer](https://azure.microsoft.com/features/storage-explorer/) to view blob containers and verify your upload is successful.
