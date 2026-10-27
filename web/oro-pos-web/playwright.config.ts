import { defineConfig } from '@playwright/test';
import {existsSync} from 'node:fs';
import {join} from 'node:path';
const installedChrome=process.platform==='win32'&&process.env.LOCALAPPDATA?join(process.env.LOCALAPPDATA,'Google','Chrome','Application','chrome.exe'):undefined;
const executable=process.env.PW_BROWSER_EXECUTABLE||(!process.env.CI&&installedChrome&&existsSync(installedChrome)?installedChrome:undefined);
export default defineConfig({
  testDir:'./e2e',timeout:45000,expect:{timeout:12000},workers:1,
  use:{baseURL:'http://127.0.0.1:4300',browserName:'chromium',channel:'chromium',headless:true,trace:'retain-on-failure',launchOptions:{executablePath:executable}},
  webServer:{command:'node node_modules/@angular/cli/bin/ng.js serve --host 127.0.0.1 --port 4300',url:'http://127.0.0.1:4300',reuseExistingServer:!process.env.CI,timeout:180000},
});
