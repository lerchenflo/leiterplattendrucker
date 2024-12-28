//const SERVER_ADDR = "http://192.168.144.48:6850";
const SERVER_ADDR = location.host;
//const SERVER_ADDR = "http://localhost:6850";

import {updateProgress} from './ui.js'


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

export function isPrinting(){
    var req = new XMLHttpRequest();
    req.open("GET", SERVER_ADDR, false);
    req.setRequestHeader("action", "isprinting");
    req.send(null);

    if(req.responseText == "True"){
        return true;
    }else{
        return false;
    }
    
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

