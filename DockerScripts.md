Start from scratch:
docker-compose down --rmi all

Create network:
docker network create --driver=bridge --subnet=192.168.2.0/24 --gateway=192.168.2.10 gcd

Create volumes:
docker volume create --name=grafana-volume
docker volume create --name=influxdb-volume

Run:
docker-compose up -d --force-recreate --build