const SERVER_ADDR = location.host;

export function ping(){
    var req = new XMLHttpRequest();
    req.open("GET", SERVER_ADDR, false);
    req.setRequestHeader("msgtype", "PING");
    req.send(null);
    //var headers = req.getAllResponseHeaders();
    
    console.log(req.responseText);
    console.log(req.getAllResponseHeaders());
    return req.responseText;
}

// send the gerber file as string
export function send_gerber(gerber_content){
    var req = new XMLHttpRequest();
    req.open("POST", SERVER_ADDR);
    req.setRequestHeader("action", "initgerberfile");
    req.send(gerber_content);
    
}

// get berber preview 
export function get_preview(){
    var req = new XMLHttpRequest();
    req.open("GET", SERVER_ADDR, false);
    req.setRequestHeader("action", "getgerberpreview");
    req.send(null);
    return req.responseText;
}

// check if printer is aready printing
export function isPrinting(){
    var req = new XMLHttpRequest();
    req.open("GET", SERVER_ADDR, false);
    req.setRequestHeader("action", "isprinting");
    req.send(null);

    return req.responseText == "True";
    
}

// functions for settings
export function set_settings(padfill, polygonfill, offsetx, offsety, mirror){
    var req = new XMLHttpRequest();
    req.open("POST", SERVER_ADDR);
    req.setRequestHeader("action", "settings");
    req.setRequestHeader("setpolygonfill", polygonfill.toString().replace("." , ","));
    req.setRequestHeader("setpadfill", padfill.toString().replace("." , ","));
    req.setRequestHeader("offsetx", offsetx.toString().replace("." , ","));
    req.setRequestHeader("offsety", offsety.toString().replace("." , ","));
    req.setRequestHeader("mirror", mirror);
    req.send(null);
}

export function start(){
    console.log("start");
    var req = new XMLHttpRequest();
    req.open("POST", SERVER_ADDR);
    req.setRequestHeader("action", "startprinting");
    req.send(null);
}

export function stop(){
    console.log("stop");
    var req = new XMLHttpRequest();
    req.open("POST", SERVER_ADDR);
    req.setRequestHeader("action", "stopprinting");
    req.send(null);
}

export function pauseprinting(){
    var req = new XMLHttpRequest();
    req.open("POST", SERVER_ADDR);
    req.setRequestHeader("action", "pauseprinting");
    req.send(null);
}

export function getPrintStatus(){
    var req = new XMLHttpRequest();
    req.open("GET", SERVER_ADDR, false);
    req.setRequestHeader("action", "getprintpercentage");

    try{
        req.send(null);
    }catch(error){
        alert("Server offline: \n" + error);
    }
    return req.responseText;
}

export function getPathPoints(){
    return null;
}

