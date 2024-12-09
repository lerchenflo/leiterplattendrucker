import {pauseprinting, send_gerber, start, stop, get_preview, getPrintStatus} from './network_task.js'


// Progress bar for Fielupload
const form = document.querySelector('form');
const progressBar = document.createElement('progress');
progressBar.value = 0;
progressBar.max = 100;
form.parentNode.insertBefore(progressBar, form.nextSibling);

export function updateProgress(){
    var val = getPrintStatus();
    var progressBar = document.getElementById("printProgressBar");
    var progressText = document.getElementById("progessText");
    progressBar.value = val;
    progressText.innerHTML = "Progress: " + progressBar.value + "%";
  }

form.addEventListener('submit', (event) => {
    event.preventDefault();

    const xhr = new XMLHttpRequest();
    xhr.open('POST', form.action);
    xhr.upload.addEventListener('progress', (event) => {
    progressBar.value = (event.loaded / event.total) * 100;
    });

    xhr.onload = () => {
    if (xhr.status === 200) {
        // Handle successful upload
        console.log('Upload successful!');
        console.log("reload")
        location.reload(); // reload page
    } else {
        // Handle upload error
        console.error('Upload failed.');
    }
    };

    const formData = new FormData(form);
    xhr.send(formData);
});


// Load Gerber file using js, sending it to the server in Textformat
const fileSelector = document.getElementById('file-selector');
  fileSelector.addEventListener('change', (event) => {
    const fileList = event.target.files;
    //console.log(fileList[0]);
    const file = fileList[0];
    if (file) {
        var reader = new FileReader();
        reader.readAsText(file, "UTF-8");
        reader.onload = function (evt) {
            var content = evt.target.result;
            //console.log(content);
            send_gerber(content);
            
        }
        reader.onerror = function (evt) {
            console.log("error reading file");
        }
    }
  });

// Draw the preview onto the canvas
function drawpreview(preview_json){
    //console.log(preview_json);
    //console.log(preview_json.length)

    // Drawing onto canvas from json array
    const canvas = document.getElementById("pcbPreview");
    const ctx = canvas.getContext("2d");

    var canvas_width = canvas.width;
    var canvas_height = canvas.height;

    
    var max_width = 0;
    for(var i=0; i < preview_json.length; i++){
        if(preview_json[i]._endpoint.X > max_width){
            max_width = preview_json[i]._endpoint.X;
        }
    }
    var max_height = 0;
    for(var i=0; i < preview_json.length; i++){
        if(preview_json[i]._endpoint.Y > max_height){
            max_height = preview_json[i]._endpoint.Y;
        }
    }

    var upscale_mulitplier = Math.min(canvas_width/max_width, canvas_height/max_height);
    //  console.log(upscale_mulitplier);

    ctx.moveTo(preview_json[0]._startpoint.X * upscale_mulitplier, canvas_height- preview_json[0]._startpoint.Y * upscale_mulitplier); // Set a start-point
    // go through every line in the json file and print the line onto the canvas
    for(var i=0; i < preview_json.length; i++){
        ctx.lineTo(preview_json[i]._endpoint.X * upscale_mulitplier, canvas_height - preview_json[i]._endpoint.Y * upscale_mulitplier);
    }

    ctx.stroke(); // Stroke it (Do the Drawing)
  
    //ctx.font = "10px Arial";
    //ctx.fillText("2",25,60);
    //ctx.fillText("10",40,25);
}
  

function test(){
    console.log("test");
}

// Start print -> button onlcick
const startprintingbtn = document.getElementById('startprinting');
startprintingbtn.onclick = function(){
    console.log("startprinting");
    start();
};

const stopprintingbtn = document.getElementById('stopprinting');
stopprintingbtn.onclick = function(){
    stop();
};

const pauseprintingbtn = document.getElementById('pauseprinting');
pauseprintingbtn.onclick = function(){
    pauseprinting();
};

const showpreviewbtn = document.getElementById('showpreview');
showpreviewbtn.onclick = function(){
    const json_string = get_preview();
    const json_object = JSON.parse(json_string);
    drawpreview(json_object);
};