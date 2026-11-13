/**
 * Copyright 2018, 2019, 2020 Miguel Müller
 * 
 * This file is part of Shieldbreakers-StreamControl-setup.
 *
 * Shieldbreakers-StreamControl-setup is free software: you can redistribute it
 * and/or modify it under the terms of the GNU General Public License as
 * published by the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * Shieldbreakers-StreamControl-setup is distributed in the hope that it will be
 * useful, but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU General
 * Public License for more details.
 *
 * You should have received a copy of the GNU General Public License along with
 * Shieldbreakers-StreamControl-setup.  If not, see <http://www.gnu.org/licenses/>.
 */

//Depends on easeljs, tweenjs.

const SCORE_X = 800;
const SCORE_Y = 7;
const SCOREsf6_X = 720;
const SCOREsf6_Y = 10;
const SCOREtkn_X = 610;
const SCOREtkn_Y = 20;
const SCOREuni_X = 250;
const SCOREuni_Y = 20;
const SCOREmk_X = 785;
const SCOREmk_Y = 20;
const SCOREkof_X = 630;
const SCOREkof_Y = 10;
const SCOREgbfri_X = 620;
const SCOREgbfri_Y = 2;
const SCOREggs_X = 600;
const SCOREggs_Y = 15;

const SCORE_MAX_WIDTH = 40;

const SCORE_FONT_SIZE = 55;
const SCOREsf6_MAX_WIDTH = 60;
const SCOREsf6_FONT_SIZE = 50;
const SCOREggs_FONT_SIZE = 70;
const SCOREgbfri_FONT_SIZE = 60;


const ROUND1_X = 960;
const ROUND1_Y = 8;
const ROUNDsf6_X = 960;
const ROUNDsf6_Y = 9;
const ROUNDggs_Y = 11;
const ROUND_X = 960;
const ROUND_Y = 10;
const ROUNDgb_Y = 10;
const ROUNDtkn_X = 800;
const ROUNDtkn_Y = 3;
const ROUNDB_X = 960;
const ROUNDB_Y = 16;
const ROUNDmk_Y = 12;
const ROUND_MAX_WIDTH = 220;
const ROUND_FONT_SIZE = 30;
const ROUNDB_FONT_SIZE = 40;
const ROUNDggs_FONT_SIZE = 40;
const ROUNDtkn_FONT_SIZE = 25;
const ROUNDsf6_FONT_SIZE = 30;
const ROUNDgb_FONT_SIZE = 20;

const textbottom_X = 955;
const textbottom_Y = 1045;
const textbottom_MAX_WIDTH = 220;
const textbottom_FONT_SIZE = 40;
const textbottomsf6_X = 960;
const textbottomsf6_Y = 1035;
const textbottomkof_Y = 1040;
const textbottomtkn_Y = 1040;
const textbottomsf6_MAX_WIDTH = 220;
const textbottomsf6_FONT_SIZE = 40;

const TOP8text_X = 960;
const TOP8text_Y = 1035;
const TOP8text_MAX_WIDTH = 1000;


const CHAR_PATH = "images/characters/";
let CHAR_HEIGHT = 1000;
let CHAR_WIDTH = CHAR_HEIGHT; //assuming src imgs are quadratic
let CHAR_CROPPED_HEIGHT = 950;

let charRect = new createjs.Rectangle(0, 0, 1380, CHAR_CROPPED_HEIGHT); //works if img width is <= 100
let charRectTopCropped = new createjs.Rectangle(0, 16, 100, CHAR_CROPPED_HEIGHT); //used to make the characters in lowChars more visible

const lowChars = ["mario", "metaknight", "pichu", "inkling", "iceclimbers"];
    
const xhr = new XMLHttpRequest();
xhr.responseType = 'json';
let animating = false;
let doUpdate = false;

let timestamp;
let timestampOld;
const dynamicTexts = new Map();
const dynamicImages = new Map();
const paths = new Map();
let stage;
function initDoubles() {
  CHAR_HEIGHT = 1000;
  CHAR_WIDTH = CHAR_HEIGHT;
  CHAR_CROPPED_HEIGHT = 950;
  charRect = new createjs.Rectangle(0, 0, 1380, CHAR_CROPPED_HEIGHT); //works if img width is <= 100
  charRectTopCropped = new createjs.Rectangle(0, 10, 500, CHAR_CROPPED_HEIGHT); //used to make the characters in lowChars more visible
}
function createScoreboard(board) {
  this.window.setInterval(pollHandler, 250);

  stage = new createjs.Stage("myCanvas");
  board.y = -100;
  stage.addChild(board);
  animating = true;
  createjs.Tween.get(board).to({y:0},800, createjs.Ease.quintOut);

  createjs.Ticker.addEventListener("tick", enterFrame);
  createjs.Ticker.framerate = 60;

  stage.update();
}
function newDynText(name, x, y, maxWidth, fontsize, fontcolor, textAlign = "center") {
  const dynamicText = new createjs.Text(
    "",
    `${fontsize}px Bebas Neue`,
    fontcolor
  );
  dynamicText.x = x;
  dynamicText.y = y;
  dynamicText.maxWidth = maxWidth;
  dynamicText.textAlign = textAlign;
  dynamicText.textBaseline = "hanging";
  dynamicTexts.set(name, dynamicText);
  return dynamicText;
}
function newMirroredDynText(name, mirrorName) {
  const dynamicText = dynamicTexts.get(mirrorName).clone();
  dynamicText.x = document.getElementById("myCanvas").width - dynamicText.x;
  if (dynamicText.textAlign == "right")
    dynamicText.textAlign = "left";
  else if (dynamicText.textAlign == "left")
    dynamicText.textAlign = "right";
  dynamicTexts.set(name, dynamicText);
  return dynamicText;
}
function newDynImage(name, x, y, path) {
  const dynamicImage = new createjs.Container();
  dynamicImage.x = x;
  dynamicImage.y = y;
  dynamicImage.name = "";
  paths.set(name, path);
  dynamicImages.set(name, dynamicImage);
  return dynamicImage;
}
function newMirroredDynImage(name, mirrorName) {
  const dynamicImage = dynamicImages.get(mirrorName).clone();
  const path = paths.get(mirrorName);
  dynamicImage.x = document.getElementById("myCanvas").width - (path==CHAR_PATH?CHAR_WIDTH:FLAG_WIDTH) - dynamicImage.x;
  paths.set(name, paths.get(mirrorName));
  dynamicImages.set(name, dynamicImage);
  return dynamicImage;
}

function pollHandler() {
  xhr.open('GET', 'streamcontrol.json');
  xhr.send();
  xhr.onload = () => {
    if (xhr.response) {
      timestampOld = timestamp;
      timestamp = xhr.response.timestamp;
      if (timestamp !== timestampOld) {
        doUpdate = true;
      }
      if (doUpdate && (!animating || timestampOld === undefined)) {
        dynamicTexts.forEach(updateText);
        dynamicImages.forEach(updateImage);
        doUpdate = false;
      }
    }
  }
}

function updateText(dynamicText, name, dynTexts) {
  let newText = xhr.response[name];
  if (dynamicText.name != null) {
      newText = getTeamName(newText, xhr.response[dynamicText.name]);
  }
  if (dynamicText.text !== newText) {
    animating = true;
    createjs.Tween.get(dynamicText)
      .to({alpha:0},500,createjs.Ease.quintIn)
      .call(function() {
        dynamicText.text = newText;
      })
      .to({alpha:1},500,createjs.Ease.quintOut)
      .call(function() {animating = false;});
  }
}

function updateImage(dynamicImage, name, dynImages) {
  const path = paths.get(name);
  let newFileName = xhr.response[name];
  if (typeof FLAG_PATH !== 'undefined' && path == FLAG_PATH)
    newFileName = getCountry(newFileName);
  if (dynamicImage.name !== newFileName) {
    animating = true;
    const prevAlpha = dynamicImage.alpha;
    const bitmap = new createjs.Bitmap(path + newFileName + ".png");
    createjs.Tween.get(dynamicImage).to({alpha:0},500, createjs.Ease.quintIn).call(function() {
      dynamicImage.removeAllChildren();
      if (bitmap.image.height != 0) { //image exists
        if (path === CHAR_PATH) {
          bitmap.sourceRect = lowChar(newFileName) ? charRectTopCropped : charRect;
          bitmap.scale = CHAR_HEIGHT / bitmap.image.height;
        } else {
          bitmap.scale = FLAG_WIDTH / bitmap.image.width;
        }
        dynamicImage.addChild(bitmap);
      }
      dynamicImage.name = newFileName;
    })
      .to({alpha:prevAlpha},500, createjs.Ease.quintOut).call(function() {animating = false;});
  }
}

function enterFrame(event) {
  if (animating)
    stage.update(event);
}

function getCountry(country) {
  if (country.length === 0) {
    return country;
  }
  const countryObj = iso.findCountryByName(country) || iso.findCountryByCode(country);
  return countryObj ? countryObj.value.toUpperCase() : 'unknown';
}
function getTeamName(name1, name2, name3) {
  return name1 + (name2==""?"":(" / "+name2));
}
function lowChar(charFileName) {
  return lowChars.includes(charFileName.split(/[1-9]/)[0]);
}
