@echo off

pushd .\Tools\httpserver
node ./src/index.js --port 8080 --path ../../out/packages
popd