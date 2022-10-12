const DaprClient = require('@dapr/dapr');

const DAPR_HOST = process.env.DAPR_HOST || "http://localhost";
const DAPR_HTTP_PORT = process.env.DAPR_HTTP_PORT || "3500";
const PUBSUB_NAME = "selfieapppubsub";
const PUBSUB_TOPIC = "incoming";

if (process.env.NODE_ENV !== 'production') {
    require('dotenv').load();
}

const
      express = require('express')
    , router = express.Router()

    , multer = require('multer')
    , inMemoryStorage = multer.memoryStorage()
    , uploadStrategy = multer({ storage: inMemoryStorage }).single('image')

    , { BlockBlobClient } = require('@azure/storage-blob')
    , getStream = require('into-stream')
    , containerName = 'images'
;

const handleError = (err, res) => {
    //res.status(500);
    res.render('error', { error: err });
};

const getBlobName = originalName => {
    var dt = new Date();
    const identifier = `${dt.getFullYear()}${dt.getMonth()}${dt.getDay()}${dt.getHours()}${dt.getMinutes()}${dt.getSeconds()}${dt.getMilliseconds()}`;
    return `${identifier}-${originalName}`;
};

router.post('/', uploadStrategy, (req, res) => {

    const
          blobName = getBlobName(req.file.originalname)
        , blobService = new BlockBlobClient(process.env.AZURE_STORAGE_CONNECTION_STRING,containerName,blobName)
        , stream = getStream(req.file.buffer)
        , streamLength = req.file.buffer.length
    ;

    blobService
        .uploadStream(stream, streamLength)
            .then(
                async (resp) => {
                    var msg = resp._response.request.url.replace('?comp=blocklist','');

                    try {
                        console.log("Creating Dapr client using host " + DAPR_HOST + " and port " + DAPR_HTTP_PORT);
                        var dapr = new DaprClient.DaprClient(DAPR_HOST, DAPR_HTTP_PORT);

                        try {
                            var selfie = { url: msg };
                            var selfieJson = JSON.stringify(selfie);
                            console.log("Publishing " + selfieJson);
                            await dapr.pubsub.publish(PUBSUB_NAME, PUBSUB_TOPIC, selfie);
                            console.log("Published data: " + selfieJson);
                        }
                        catch(publishError) {
                            console.log("Error publishing message: ");
                            console.log(publishError);
                        }
                    }
                    catch(clientError) {
                        console.log("Error creating Dapr client using host " + DAPR_HOST + " and port " + DAPR_HTTP_PORT);
                        console.log(clientError);
                    }

                    res.sendStatus(200);
                    //res.redirect("/");
                }
            )
            .catch(
                (err)=>{
                if(err) {
                    handleError(err);
                    return;
                }
            })
        });

module.exports = router;