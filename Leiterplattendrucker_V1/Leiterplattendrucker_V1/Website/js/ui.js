import {pauseprinting, send_gerber, start, stop, get_preview, getPrintStatus, isPrinting} from './network_task.js';

// Update the progressbar and the Text below according to the print status
export function updateProgress(){
    var val = getPrintStatus();
    var progressBar = document.getElementById("printProgressBar");
    var progressText = document.getElementById("progessText");
    progressBar.value = val;
    progressText.innerHTML = "Fortschritt: " + progressBar.value + "%";
  }

export function drawPreviewFromServer(indexpage=true, alertBool=false){ // gets the Preview from the Server and displays it, when buttonexits = true it disables the start button from index.html
    const json_string = get_preview();
    
    if (json_string == "Keine Preview")
    {
        if(alertBool){
            alert("Falsches Gerberfile format oder sehr großes Gerberfile");
        }
        if(indexpage){
            document.getElementById("startprinting").disabled = true; // disable start button
            document.getElementById("startprinting").title = "Bitte zuerst eine korrekte Gerberdatei hochladen";
            document.getElementById("file-selector").value = '';
            const canvas = document.getElementById("pcbPreview");// clear canvas
            canvas.getContext("2d").clearRect(0, 0, canvas.width, canvas.height); 
        }if(alertBool && indexpage){
            const fileLoadingCircle = document.getElementById("fileLoadingCircle"); //disable the loading circle 
            fileLoadingCircle.style.visibility = "hidden";
        }
    } else if (json_string == ""){
        alert("Server nicht erreichbar")
        location.reload();
    }
    else
    {
        const json_object = JSON.parse(json_string);
        drawpreview(json_object);
        if(indexpage){
            document.getElementById("startprinting").disabled = false;
            document.getElementById("startprinting").title = "Startet den Druckvorgang";
            const fileLoadingCircle = document.getElementById("fileLoadingCircle"); //disable the loading circle 
            fileLoadingCircle.style.visibility = "hidden";
        } 
    }
}

export function redirectIfPrinting(){
    let newURL = "";
    if(isPrinting()){ //redirect to printstatus if Printer is already Printing
        newURL = "/printstatus.html";
    }else{
        newURL = "/";
    }
    if(!(window.location.href).endsWith(newURL)){
        window.location.href = "." + newURL;
        return true;
    }else{
        return false;
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
