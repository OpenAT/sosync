#!/bin/bash

echo -e "\e[93mPublishing solution...\e[39m"
dotnet restore
dotnet publish -c Release -o ./../bin/Publish
