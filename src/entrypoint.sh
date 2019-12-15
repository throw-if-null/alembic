#!/usr/bin/env sh

set -e
set -o pipefail

DOCKER_SOCK=${DOCKER_SOCK:-/var/run/docker.sock}
CURL_TIMEOUT=${CURL_TIMEOUT:-30}

drawIntro() {
	echo "            \`-+oyyhhys+/-\`                         "
	echo "         -odMMMMMMMMMMMMMMNdys+:-\`                 "
	echo "       :dMMMMMMMMMMMMMMMMMMMMMMMMMNdhs+/-\`         "
	echo "     .hMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMMNmhs.    "
	echo "    -mMMMMMMMMMMMMMMMMMMMMMMMMMMmoshdNMMMMMMMMd    "
	echo "   \`mMMMMMMMMMMMMMMMMMMMMMMMMMMMM+    \`-:+syhy-    "
	echo "   oMMMMMMMMMMMMMMMMMMMMMMMMMMMMMN\`                "
	echo "   dMMMMMMMMMMMMMMMMMMMMMMMMMMMMMM:                "
	echo "   mmsssssssssssssssssssssssssssyM/                "
	echo "   sNooooooooossoooooooooooooooohN.                "
	echo "   .NhooooosssooooooooooooooooosNy                 "
	echo "    /Nhooooyyyoooooooooooooooosmd\`                 "
	echo "     :mdsooooossoooooooooooooyNy\`                  "
	echo "      -hdysssssssssssssssssshmo                    "
	echo "      .--yymdhyssssssssydmhy+--                    "
	echo "        :y.\`:smhddmmdddd/. +s                      "
	echo "        s+   oo       .y-  .y:                     "
	echo "       :y.  -y.        oo   +s                     "
	echo "       s+   \`.         \`.   .y:                    "
	echo "      :y.                    +s                    "
	echo "      -:                     \`/\`                   "
}

# SIGTERM-handler
term_handler() {
  exit 143; # 128 + 15 -- SIGTERM
}

docker_curl() {
  curl --max-time "${CURL_TIMEOUT}" --no-buffer -s --unix-socket "${DOCKER_SOCK}" $@ || return 1
  return 0
}

trap 'kill ${!}; term_handler' SIGTERM

if [ -e ${DOCKER_SOCK} ]; then
	
	drawIntro
	# https://docs.docker.com/engine/api/v1.25/

	# Set container selector
	if [ "$AUTOHEAL_CONTAINER_LABEL" == "all" ]; then
		labelFilter=""
	else
		labelFilter=",\"label\":\[\"${AUTOHEAL_CONTAINER_LABEL:=autoheal}=true\"\]"
	fi

	echo "Label filter is: $AUTOHEAL_CONTAINER_LABEL"
	
	AUTOHEAL_START_PERIOD=${AUTOHEAL_START_PERIOD:=0}
	echo "Monitoring containers for unhealthy status in ${AUTOHEAL_START_PERIOD} second(s)"
	sleep ${AUTOHEAL_START_PERIOD}

	while true; do
		sleep ${AUTOHEAL_INTERVAL:=5}
	
		apiUrl="http://localhost/containers/json?filters=\{\"health\":\[\"unhealthy\"\]${labelFilter}\}"
		stopTimeout=".Labels[\"autoheal.stop.timeout\"] // ${AUTOHEAL_DEFAULT_STOP_TIMEOUT:-10}"
		docker_curl "$apiUrl" | \
		jq -r "foreach .[] as \$CONTAINER([];[]; \$CONTAINER | .Id, .Names[0], $stopTimeout )" | \
		while read -r CONTAINER_ID && read -r CONTAINER_NAME && read -r TIMEOUT; do
			CONTAINER_SHORT_ID=${CONTAINER_ID:0:12}
			DATE=$(date +%d-%m-%Y" "%H:%M:%S)
			
			if [ "null" = "$CONTAINER_NAME" ]; then
				echo "$DATE Container name of ($CONTAINER_SHORT_ID) is null, which implies container does not exist - don't restart"
			else
				echo "$DATE Container ${CONTAINER_NAME} ($CONTAINER_SHORT_ID) found to be unhealthy - Restarting container now with ${TIMEOUT}s timeout"
				docker_curl -f -XPOST "http://localhost/containers/${CONTAINER_ID}/restart?t=${TIMEOUT}" \
				|| echo "$DATE Restarting container $CONTAINER_SHORT_ID failed"
			fi
		done
	done

else
	echo $DOCKER_SOCK
	exec "$@"
fi;