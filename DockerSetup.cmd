@ECHO OFF

:: This CMD script setup all Docker containers for cluster and node.

TITLE GrandCentralDispatch Docker Setup

ECHO Please wait... Creating Docker network bridge.
docker network create --driver=bridge --subnet=192.168.2.0/24 --gateway=192.168.2.10 gcd-network

ECHO Please wait... Creating Docker volumes.
docker volume create --name=grafana-volume
docker volume create --name=influxdb-volume
docker volume create --name=elasticsearch-volume

ECHO Please wait... Deploying GCD Node.
cd GrandCentralDispatch.Node/
docker-compose down --rmi all
docker-compose up -d --force-recreate --build

ECHO Please wait... Deploying GCD Cluster.
cd ../GrandCentralDispatch.Cluster/
docker-compose down --rmi all
docker-compose up -d --force-recreate --build
PAUSE