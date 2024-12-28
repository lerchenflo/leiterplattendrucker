import {pauseprinting, send_gerber, start, stop, get_preview, getPrintStatus} from './network_task.js';

// Update the progressbar and the Text below according to the print status
export function updateProgress(){
    var val = getPrintStatus();
    var progressBar = document.getElementById("printProgressBar");
    var progressText = document.getElementById("progessText");
    progressBar.value = val;
    progressText.innerHTML = "Progress: " + progressBar.value + "%";
  }





export function drawPreviewFromServer(buttonExists=true){ // gets the Preview from the Server and displays it, when buttonexits = true it disables the start button from index.html
    const json_string = get_preview();
    
    if (json_string == "Keine Preview")
    {
        alert("Gerberfile wurde nicht gefunden")
        if(buttonExists){
            document.getElementById("startprinting").disabled = true;
        }     
    }
    else
    {
        const json_object = JSON.parse(json_string);
        console.log(json_object);
        drawpreview(json_object);
        if(buttonExists){
            document.getElementById("startprinting").disabled = false;
        } 
    }
}



// Load Gerber file using js, sending it to the server in Textformat
const fileSelector = document.getElementById('file-selector');
fileSelector.addEventListener('change', (event) => {
    fileupload_event = event;
    load_and_send_gerber();
  });

let fileupload_event = null;

function load_and_send_gerber() {
    const fileList = fileupload_event.target.files;
    //console.log(fileList[0]);
    const file = fileList[0];
    if (file) {
        var reader = new FileReader();
        reader.readAsText(file, "UTF-8");
        reader.onload = function (evt) {
            var content = evt.target.result;
            //console.log(content);
            send_gerber(content);

            setTimeout(() => {
                get_and_draw_preview();
            }, 500);

        }
        reader.onerror = function (evt) {
            console.log("error reading file");
        }
    }
}


// Draw the preview onto the canvas
function drawpreview(preview_json) {
    // Drawing onto canvas from json array
    const canvas = document.getElementById("pcbPreview");
    const ctx = canvas.getContext("2d");

    // Clear the canvas
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    var canvas_width = canvas.width;
    var canvas_height = canvas.height;

    var max_width = 0;
    for (var i = 0; i < preview_json.length; i++) {
        if (preview_json[i]._endpoint.X > max_width) {
            max_width = preview_json[i]._endpoint.X;
        }
    }
    var max_height = 0;
    for (var i = 0; i < preview_json.length; i++) {
        if (preview_json[i]._endpoint.Y > max_height) {
            max_height = preview_json[i]._endpoint.Y;
        }
    }

    var upscale_multiplier = Math.min(canvas_width / max_width, canvas_height / max_height);

    // Loop through the lines and draw based on 'paint' value
    for (var i = 0; i < preview_json.length; i++) {
        // Set stroke color based on 'paint' value
        if (preview_json[i]._paint === true) {
            ctx.strokeStyle = 'gold';  // Gold for paint = true
        } else {
            ctx.strokeStyle = 'red';  // Red for paint = false
        }

        // Start a new path for the current line
        ctx.beginPath();
        ctx.moveTo(
            preview_json[i]._startpoint.X * upscale_multiplier,
            canvas_height - preview_json[i]._startpoint.Y * upscale_multiplier
        );

        // Draw the current line to the endpoint
        ctx.lineTo(
            preview_json[i]._endpoint.X * upscale_multiplier,
            canvas_height - preview_json[i]._endpoint.Y * upscale_multiplier
        );

        // Stroke the current line
        ctx.stroke();
    }
}

function get_and_draw_preview() {
    const json_string = get_preview();

    if (json_string == "Keine Preview") {
        if (!fileupload_event) {
            load_and_send_gerber();
            get_and_draw_preview();
        }
        
    }
    else {
        const json_object = JSON.parse(json_string);
        console.log(json_object);
        drawpreview(json_object);
    }
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

/*
const stopprintingbtn = document.getElementById('stopprinting');
stopprintingbtn.onclick = function(){
    stop();
};

const pauseprintingbtn = document.getElementById('pauseprinting');
pauseprintingbtn.onclick = function(){
    pauseprinting();
};

*/

