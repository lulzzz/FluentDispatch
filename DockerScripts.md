Create network:
docker network create --driver=bridge --subnet=192.168.2.0/24 --gateway=192.168.2.10 gcd

Create volumes;
docker volume create grafana-volume
docker volume create influxdb-volume