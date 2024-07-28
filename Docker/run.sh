if [ -z "${IMG_TAG}" ]; then
  IMG_TAG='v2.0.6'
fi

echo Using image tag $IMG_TAG

if [ ! -f "restdb.json" ]
then
  echo Configuration file restdb.json not found.
  exit
fi

# Items that require persistence
#   restdb.json
#   sample.db
#   logs/

# Argument order matters!

docker run \
  -p 8000:8000 \
  -t \
  -i \
  -e "TERM=xterm-256color" \
  -v ./restdb.json:/app/restdb.json \
  -v ./sample.db:/app/sample.db \
  -v ./logs/:/app/logs/ \
  jchristn/restdb:$IMG_TAG
