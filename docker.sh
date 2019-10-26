#!/bin/sh
cd GrandCentralDispatch.Node/
docker-compose down --rmi all
docker-compose up -d --force-recreate --build

cd ../GrandCentralDispatch.Cluster/
docker-compose down --rmi all
docker network create --driver=bridge --subnet=192.168.2.0/24 --gateway=192.168.2.10 gcd
docker volume create --name=grafana-volume
docker volume create --name=influxdb-volume
docker-compose up -d --force-recreate --build
