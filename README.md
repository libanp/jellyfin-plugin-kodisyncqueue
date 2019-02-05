# Kodi Companion Plugin for Jellyfin

## Build

* Install dotnet core sdk 2.2 by following instructions [here](https://dotnet.microsoft.com/download)
* `git clone https://github.com/jellyfin/jellyfin-plugin-kodisyncqueue`
* `cd jellyfin-plugin-kodisyncqueue`
* `git submodule update --init`
* `dotnet build`
* Build output is in `./bin/`

## Install

* Copy the built .dll file to the Jellyfin plugin directory (`/var/lib/jellyfin/plugins' on linux)
* Restart Jellyfin server
