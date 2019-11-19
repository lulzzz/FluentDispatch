#!/bin/sh
echo "Please wait... Creating Docker network bridge."
docker network create --driver=bridge --subnet=192.168.2.0/24 --gateway=192.168.2.10 fluentdispatch-network

echo "Please wait... Creating Docker volumes."
docker volume create --name=grafana-volume
docker volume create --name=influxdb-volume
docker volume create --name=elasticsearch-volume

echo "Please wait... Deploying FluentDispatch Node."
cd FluentDispatch.Node/
docker-compose down --rmi all
docker-compose up -d --force-recreate --build

echo "Please wait... Deploying FluentDispatch Cluster."
cd ../FluentDispatch.Cluster/
docker-compose down --rmi all
docker-compose up -d --force-recreate --build
