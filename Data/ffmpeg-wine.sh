#!/usr/bin/env bash

PORT="$1"
should_exit=0

[[ -z "$PORT" ]] && should_exit=1

is_port_in_use() {
  if netstat -tuln | grep -q ":$1 "; then
    return 0
  elif ss -tuln | grep -q ":$1 "; then
    return 0
  else
    return 1
  fi
}

check_dependencies() {
  command -v ffmpeg >/dev/null 2>&1 || should_exit=1
  command -v nc >/dev/null 2>&1 || should_exit=1

  # We don't support GNU netcat
  if nc -h 2>&1 | grep -q "GNU"; then
    should_exit=1
  fi

  if is_port_in_use $PORT; then
    should_exit=1
  fi
}
check_dependencies

run_command() {
  local cmd="$1"

  if [[ "$cmd" == "exit" ]]; then
    should_exit=1
    return
  fi

  if [[ "$cmd" =~ ^ffmpeg[[:space:]] ]]; then
    eval "$cmd" > /dev/null 2>&1
  fi
}

while [[ $should_exit -eq 0 ]] ; do
  { 
    read -r cmd
    if [[ -n "$cmd" ]]; then
      run_command "$cmd"
    fi
  } < <(nc -l 127.0.0.1 $PORT)
done
