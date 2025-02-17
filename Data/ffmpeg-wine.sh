#!/usr/bin/env bash

PORT="$1"
should_exit=0
[[ -z "$PORT" ]] && exit 1 # exit early, otherwise kill_orphaned_nc could kill other netcat processes

is_port_in_use() {
  PORT=$1
  if [[ "$OSTYPE" == "darwin"* ]]; then
    if netstat -an | grep -q "LISTEN" | grep -q ":$PORT "; then
      return 0
    fi
  else
    if ss -tuln | grep -q ":$PORT "; then
      return 0
    fi
  fi
  return 1
}

check_dependencies() {
  # while most of these commands should be preinstalled on any sane distro,
  # there exists nixos/arch where the users can be... well, users.
  command -v netstat >/dev/null 2>&1 || command -v ss >/dev/null 2>&1 || should_exit=1
  command -v ffmpeg >/dev/null 2>&1 || should_exit=1
  command -v pgrep >/dev/null 2>&1 || should_exit=1
  command -v grep >/dev/null 2>&1 || should_exit=1
  command -v nc >/dev/null 2>&1 || should_exit=1
  command -v wc >/dev/null 2>&1 || should_exit=1

  if nc -h 2>&1 | grep -q "GNU"; then
    echo "We don't support GNU netcat"
    should_exit=1
  fi

  if is_port_in_use $PORT; then
    echo "Port $PORT is already in use"
    should_exit=1
  fi
}

kill_orphaned_nc() {
  # nc ocasionally gets orphaned, kill it if there aren't two
  # instances of ffmpeg-wine.sh (this one and another running nc)
  instances_of_ffmpeg_wine=$(pgrep -f "ffmpeg-wine.sh" | grep -v $$ | wc -l)
  if [[ $instances_of_ffmpeg_wine -ne 2 ]]; then
    nc_pid=$(pgrep -f "nc -l 127.0.0.1 $PORT")
    if [[ -n "$nc_pid" ]]; then
      kill "$nc_pid" 2>/dev/null
    fi
  fi
}

run_command() {
  local cmd="$1"
  echo "Received command: '$cmd'"

  if [[ "$cmd" == "exit" ]]; then
    echo "Exiting..."
    should_exit=1
    return
  fi

  if [[ "$cmd" =~ ^ffmpeg[[:space:]] ]]; then
    echo "Executing command: '$cmd'"
    eval "$cmd" > /dev/null 2>&1
  fi
}

main() {
  check_dependencies
  kill_orphaned_nc

  echo "Starting daemon on port $PORT. Will exit immediately if any dependencies are unmet."
  while [[ $should_exit -eq 0 ]] ; do
    {
      read -r cmd
      echo "New connection opened"
      if [[ -n "$cmd" ]]; then
        run_command "$cmd"
      fi
      echo "Connection closed"
    } < <(nc -N -l 127.0.0.1 $PORT)
  done
}

main
