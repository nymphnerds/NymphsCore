#!/usr/bin/env bash

managed_repo_reset_state() {
  MANAGED_REPO_NAME=""
  MANAGED_REPO_PATH=""
  MANAGED_REPO_URL=""
  MANAGED_REPO_BRANCH=""
  MANAGED_REPO_STATE=""
  MANAGED_REPO_MESSAGE=""
  MANAGED_REPO_LOCAL_COMMIT=""
  MANAGED_REPO_REMOTE_COMMIT=""
  MANAGED_REPO_CURRENT_BRANCH=""
  MANAGED_REPO_AHEAD_COUNT=0
  MANAGED_REPO_BEHIND_COUNT=0
}

: "${NYMPHS3D_MANAGED_REPO_FETCH_TIMEOUT_SECONDS:=20}"

managed_repo_run_git() {
  local token="${NYMPHS3D_GITHUB_TOKEN:-${GITHUB_TOKEN:-}}"
  local askpass_file=""
  local exit_code=0

  if [[ -z "${token}" ]]; then
    GIT_TERMINAL_PROMPT=0 "$@"
    return $?
  fi

  askpass_file="$(mktemp)"
  cat > "${askpass_file}" <<'EOF'
#!/usr/bin/env bash
case "$1" in
  *Username*)
    printf '%s\n' "x-access-token"
    ;;
  *Password*)
    printf '%s\n' "${NYMPHS3D_GITHUB_TOKEN:-${GITHUB_TOKEN:-}}"
    ;;
  *)
    printf '\n'
    ;;
esac
EOF
  chmod 700 "${askpass_file}"

  GIT_TERMINAL_PROMPT=0 GIT_ASKPASS="${askpass_file}" "$@" || exit_code=$?
  rm -f "${askpass_file}"
  return "${exit_code}"
}

managed_repo_print_state() {
  printf 'repo=%s|state=%s|path=%s|branch=%s|local=%s|remote=%s|message=%s\n' \
    "${MANAGED_REPO_NAME}" \
    "${MANAGED_REPO_STATE}" \
    "${MANAGED_REPO_PATH}" \
    "${MANAGED_REPO_CURRENT_BRANCH:-${MANAGED_REPO_BRANCH}}" \
    "${MANAGED_REPO_LOCAL_COMMIT:-unknown}" \
    "${MANAGED_REPO_REMOTE_COMMIT:-unknown}" \
    "${MANAGED_REPO_MESSAGE}"
}

managed_repo_human_summary() {
  case "${MANAGED_REPO_STATE}" in
    up_to_date)
      echo "${MANAGED_REPO_NAME}: up to date (${MANAGED_REPO_LOCAL_COMMIT})."
      ;;
    behind_clean)
      echo "${MANAGED_REPO_NAME}: update available (${MANAGED_REPO_LOCAL_COMMIT} -> ${MANAGED_REPO_REMOTE_COMMIT})."
      ;;
    missing)
      echo "${MANAGED_REPO_NAME}: missing and will need to be cloned."
      ;;
    dirty)
      echo "${MANAGED_REPO_NAME}: skipped auto-update because the repo has local changes."
      ;;
    detached)
      echo "${MANAGED_REPO_NAME}: skipped auto-update because the repo is detached."
      ;;
    diverged)
      echo "${MANAGED_REPO_NAME}: skipped auto-update because the repo has diverged from origin/${MANAGED_REPO_BRANCH}."
      ;;
    ahead_local)
      echo "${MANAGED_REPO_NAME}: keeping local checkout because it is already ahead of origin/${MANAGED_REPO_BRANCH}."
      ;;
    branch_mismatch)
      echo "${MANAGED_REPO_NAME}: skipped auto-update because the repo is on ${MANAGED_REPO_CURRENT_BRANCH}, expected ${MANAGED_REPO_BRANCH}. Older installs may still be on a previous branch. Switch this repo to ${MANAGED_REPO_BRANCH} or rerun repair/reinstall with the current installer package."
      ;;
    remote_mismatch)
      echo "${MANAGED_REPO_NAME}: skipped auto-update because origin does not match the expected repo URL."
      ;;
    unsafe_ownership)
      echo "${MANAGED_REPO_NAME}: could not check updates because git rejected the repo ownership as unsafe."
      ;;
    fetch_failed)
      echo "${MANAGED_REPO_NAME}: could not check updates because git fetch failed."
      ;;
    fetch_timed_out)
      echo "${MANAGED_REPO_NAME}: could not check updates because GitHub did not respond before the timeout."
      ;;
    missing_origin)
      echo "${MANAGED_REPO_NAME}: could not check updates because the repo has no origin remote."
      ;;
    missing_remote_ref)
      echo "${MANAGED_REPO_NAME}: could not check updates because origin/${MANAGED_REPO_BRANCH} was not found."
      ;;
    not_git)
      echo "${MANAGED_REPO_NAME}: path exists but is not a git checkout."
      ;;
    *)
      echo "${MANAGED_REPO_NAME}: state=${MANAGED_REPO_STATE}."
      ;;
  esac
}

managed_repo_fetch() {
  local repo_path="$1"
  local fetch_output

  if command -v timeout >/dev/null 2>&1; then
    fetch_output="$(managed_repo_run_git timeout "${NYMPHS3D_MANAGED_REPO_FETCH_TIMEOUT_SECONDS}" git -C "${repo_path}" fetch --all --prune 2>&1)"
    case $? in
      0)
        return 0
        ;;
      124)
        MANAGED_REPO_STATE="fetch_timed_out"
        MANAGED_REPO_MESSAGE="git fetch exceeded ${NYMPHS3D_MANAGED_REPO_FETCH_TIMEOUT_SECONDS}s while contacting GitHub."
        return 1
        ;;
      *)
        MANAGED_REPO_STATE="fetch_failed"
        MANAGED_REPO_MESSAGE="${fetch_output//$'\n'/ }"
        return 1
        ;;
    esac
  fi

  if ! fetch_output="$(managed_repo_run_git git -C "${repo_path}" fetch --all --prune 2>&1)"; then
    MANAGED_REPO_STATE="fetch_failed"
    MANAGED_REPO_MESSAGE="${fetch_output//$'\n'/ }"
    return 1
  fi

  return 0
}

managed_repo_clone() {
  local repo_name="$1"
  local repo_path="$2"
  local repo_url="$3"
  local repo_branch="$4"
  local clone_pid=""
  local elapsed_seconds=0
  local heartbeat_seconds="${NYMPHS3D_MANAGED_REPO_CLONE_HEARTBEAT_SECONDS:-10}"
  local low_speed_limit="${NYMPHS3D_MANAGED_REPO_CLONE_LOW_SPEED_LIMIT:-1024}"
  local low_speed_time="${NYMPHS3D_MANAGED_REPO_CLONE_LOW_SPEED_TIME:-60}"

  echo "Cloning ${repo_name} from ${repo_url} into ${repo_path}"
  managed_repo_run_git git \
    -c "http.lowSpeedLimit=${low_speed_limit}" \
    -c "http.lowSpeedTime=${low_speed_time}" \
    clone --progress --branch "${repo_branch}" --single-branch "${repo_url}" "${repo_path}" &
  clone_pid=$!

  while kill -0 "${clone_pid}" 2>/dev/null; do
    sleep "${heartbeat_seconds}"
    if kill -0 "${clone_pid}" 2>/dev/null; then
      elapsed_seconds=$((elapsed_seconds + heartbeat_seconds))
      echo "Still cloning ${repo_name} (${elapsed_seconds}s elapsed)..."
    fi
  done

  if ! wait "${clone_pid}"; then
    echo "Clone failed for ${repo_name}."
    return 1
  fi

  return 0
}

managed_repo_remove_path() {
  local repo_name="$1"
  local repo_path="$2"

  case "${repo_path}" in
    ""|"/"|"/home"|"/home/"|"${HOME}"|"${HOME}/"|"."|"..")
      echo "Refusing to remove unsafe managed repo path for ${repo_name}: ${repo_path:-<empty>}"
      return 1
      ;;
  esac

  case "${repo_path}" in
    "${HOME}/"*|"/home/nymph/"*)
      ;;
    *)
      echo "Refusing to remove managed repo path outside the installer home for ${repo_name}: ${repo_path}"
      return 1
      ;;
  esac

  echo "Removing incomplete managed ${repo_name} checkout at ${repo_path}"
  rm -rf -- "${repo_path}"
}

managed_repo_repair_checkout() {
  local repo_name="$1"
  local repo_path="$2"
  local repo_url="$3"
  local repo_branch="$4"

  if [[ ! -d "${repo_path}/.git" ]] || ! git -C "${repo_path}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    echo "${repo_name}: cannot repair ${repo_path} because it is not a readable git checkout."
    return 1
  fi

  echo "Repairing ${repo_name} checkout in place with git fetch + reset."
  if git -C "${repo_path}" remote get-url origin >/dev/null 2>&1; then
    managed_repo_run_git git -C "${repo_path}" remote set-url origin "${repo_url}"
  else
    managed_repo_run_git git -C "${repo_path}" remote add origin "${repo_url}"
  fi
  managed_repo_run_git git -C "${repo_path}" fetch origin "${repo_branch}" --prune
  git -C "${repo_path}" checkout -B "${repo_branch}" "origin/${repo_branch}"
  git -C "${repo_path}" reset --hard "origin/${repo_branch}"
  git -C "${repo_path}" clean -fd
}

managed_repo_is_effectively_dirty() {
  local repo_path="$1"
  local repo_basename=""
  local tracked_changes=""
  local untracked_changes=""
  local filtered_untracked=""
  local ignored_untracked_regex='^(gradio_cache_tex/|gradio_cache/|Prompts/|output/|outputs/|__pycache__/)(.*)?$'

  repo_basename="$(basename "${repo_path}")"
  if [[ "${repo_basename}" == "TRELLIS.2" ]]; then
    ignored_untracked_regex='^(gradio_cache_tex/|gradio_cache/|Prompts/|output/|outputs/|__pycache__/|models/|scripts/)(.*)?$'
  fi

  tracked_changes="$(git -C "${repo_path}" status --porcelain --untracked-files=no)"
  if [[ -n "${tracked_changes}" ]]; then
    return 0
  fi

  # Ignore git-ignored runtime/cache outputs when deciding whether auto-update is safe.
  untracked_changes="$(git -C "${repo_path}" ls-files --others --exclude-standard)"
  filtered_untracked="$(printf '%s\n' "${untracked_changes}" | grep -E -v "${ignored_untracked_regex}" || true)"
  if [[ -n "${filtered_untracked}" ]]; then
    return 0
  fi

  return 1
}

managed_repo_inspect() {
  local repo_name="$1"
  local repo_path="$2"
  local repo_url="$3"
  local repo_branch="$4"
  local remote_url=""
  local remote_url_output=""
  local remote_ref=""
  local porcelain=""
  local counts=""

  managed_repo_reset_state
  MANAGED_REPO_NAME="${repo_name}"
  MANAGED_REPO_PATH="${repo_path}"
  MANAGED_REPO_URL="${repo_url}"
  MANAGED_REPO_BRANCH="${repo_branch}"
  MANAGED_REPO_CURRENT_BRANCH="${repo_branch}"

  if [[ ! -e "${repo_path}" ]]; then
    MANAGED_REPO_STATE="missing"
    MANAGED_REPO_MESSAGE="Repo directory does not exist yet."
    return 0
  fi

  if [[ ! -d "${repo_path}/.git" ]] || ! git -C "${repo_path}" rev-parse --is-inside-work-tree >/dev/null 2>&1; then
    MANAGED_REPO_STATE="not_git"
    MANAGED_REPO_MESSAGE="Path exists but is not a git checkout."
    return 0
  fi

  remote_url_output="$(git -C "${repo_path}" remote get-url origin 2>&1 || true)"
  if [[ "${remote_url_output}" == *"detected dubious ownership"* ]]; then
    MANAGED_REPO_STATE="unsafe_ownership"
    MANAGED_REPO_MESSAGE="Repo ownership is considered unsafe by git; safe.directory or ownership normalization is required."
    return 0
  fi
  if [[ "${remote_url_output}" == *"not a git repository"* ]]; then
    MANAGED_REPO_STATE="not_git"
    MANAGED_REPO_MESSAGE="Path exists but git cannot read it as a repository."
    return 0
  fi

  remote_url="${remote_url_output}"
  if [[ -z "${remote_url}" ]]; then
    MANAGED_REPO_STATE="missing_origin"
    MANAGED_REPO_MESSAGE="Repo has no origin remote."
    return 0
  fi

  if [[ "${remote_url}" != "${repo_url}" ]]; then
    MANAGED_REPO_STATE="remote_mismatch"
    MANAGED_REPO_MESSAGE="Origin remote is ${remote_url}, expected ${repo_url}."
    MANAGED_REPO_LOCAL_COMMIT="$(git -C "${repo_path}" rev-parse --short HEAD 2>/dev/null || true)"
    MANAGED_REPO_CURRENT_BRANCH="$(git -C "${repo_path}" symbolic-ref --quiet --short HEAD 2>/dev/null || echo detached)"
    return 0
  fi

  if ! managed_repo_fetch "${repo_path}"; then
    MANAGED_REPO_LOCAL_COMMIT="$(git -C "${repo_path}" rev-parse --short HEAD 2>/dev/null || true)"
    MANAGED_REPO_CURRENT_BRANCH="$(git -C "${repo_path}" symbolic-ref --quiet --short HEAD 2>/dev/null || echo detached)"
    return 0
  fi

  MANAGED_REPO_LOCAL_COMMIT="$(git -C "${repo_path}" rev-parse --short HEAD 2>/dev/null || true)"
  MANAGED_REPO_CURRENT_BRANCH="$(git -C "${repo_path}" symbolic-ref --quiet --short HEAD 2>/dev/null || echo detached)"
  remote_ref="origin/${repo_branch}"
  MANAGED_REPO_REMOTE_COMMIT="$(git -C "${repo_path}" rev-parse --short "${remote_ref}" 2>/dev/null || true)"

  if [[ "${MANAGED_REPO_CURRENT_BRANCH}" == "detached" ]]; then
    MANAGED_REPO_STATE="detached"
    MANAGED_REPO_MESSAGE="Repo is in detached HEAD state."
    return 0
  fi

  if [[ "${MANAGED_REPO_CURRENT_BRANCH}" != "${repo_branch}" ]]; then
    MANAGED_REPO_STATE="branch_mismatch"
    MANAGED_REPO_MESSAGE="Repo is on ${MANAGED_REPO_CURRENT_BRANCH}, expected ${repo_branch}. Older installs may still be on a previous branch. Switch this repo to ${repo_branch} or rerun repair/reinstall with the current installer package."
    return 0
  fi

  if [[ -z "${MANAGED_REPO_REMOTE_COMMIT}" ]]; then
    MANAGED_REPO_STATE="missing_remote_ref"
    MANAGED_REPO_MESSAGE="Remote branch ${remote_ref} was not found."
    return 0
  fi

  if managed_repo_is_effectively_dirty "${repo_path}"; then
    MANAGED_REPO_STATE="dirty"
    MANAGED_REPO_MESSAGE="Repo has local changes; skipping auto-update."
    return 0
  fi

  counts="$(git -C "${repo_path}" rev-list --left-right --count HEAD...${remote_ref})"
  IFS=$'\t' read -r MANAGED_REPO_AHEAD_COUNT MANAGED_REPO_BEHIND_COUNT <<< "${counts}"

  if [[ "${MANAGED_REPO_AHEAD_COUNT}" -gt 0 && "${MANAGED_REPO_BEHIND_COUNT}" -gt 0 ]]; then
    MANAGED_REPO_STATE="diverged"
    MANAGED_REPO_MESSAGE="Repo has diverged from ${remote_ref}."
    return 0
  fi

  if [[ "${MANAGED_REPO_AHEAD_COUNT}" -gt 0 ]]; then
    MANAGED_REPO_STATE="ahead_local"
    MANAGED_REPO_MESSAGE="Repo is ahead of ${remote_ref}; keeping local checkout."
    return 0
  fi

  if [[ "${MANAGED_REPO_BEHIND_COUNT}" -gt 0 ]]; then
    MANAGED_REPO_STATE="behind_clean"
    MANAGED_REPO_MESSAGE="Repo can be fast-forwarded to ${remote_ref}."
    return 0
  fi

  MANAGED_REPO_STATE="up_to_date"
  MANAGED_REPO_MESSAGE="Repo already matches ${remote_ref}."
  return 0
}

managed_repo_apply() {
  local repo_name="$1"
  local repo_path="$2"
  local repo_url="$3"
  local repo_branch="$4"
  local previous_commit=""

  managed_repo_inspect "${repo_name}" "${repo_path}" "${repo_url}" "${repo_branch}"
  previous_commit="${MANAGED_REPO_LOCAL_COMMIT}"

  case "${MANAGED_REPO_STATE}" in
    missing)
      managed_repo_clone "${repo_name}" "${repo_path}" "${repo_url}" "${repo_branch}"
      managed_repo_inspect "${repo_name}" "${repo_path}" "${repo_url}" "${repo_branch}"
      if [[ "${MANAGED_REPO_STATE}" != "up_to_date" && "${MANAGED_REPO_STATE}" != "fetch_timed_out" ]]; then
        echo "Managed repo clone verification failed for ${repo_name}."
        managed_repo_print_state
        return 1
      fi
      MANAGED_REPO_MESSAGE="Repo cloned at ${MANAGED_REPO_LOCAL_COMMIT}."
      managed_repo_human_summary
      managed_repo_print_state
      return 0
      ;;
    behind_clean)
      echo "Fast-forwarding ${repo_name} from ${previous_commit:-unknown} to ${MANAGED_REPO_REMOTE_COMMIT}"
      git -C "${repo_path}" merge --ff-only "origin/${repo_branch}"
      managed_repo_inspect "${repo_name}" "${repo_path}" "${repo_url}" "${repo_branch}"
      if [[ "${MANAGED_REPO_STATE}" != "up_to_date" ]]; then
        echo "Managed repo fast-forward verification failed for ${repo_name}."
        managed_repo_print_state
        return 1
      fi
      MANAGED_REPO_MESSAGE="Repo updated from ${previous_commit:-unknown} to ${MANAGED_REPO_LOCAL_COMMIT}."
      managed_repo_human_summary
      managed_repo_print_state
      return 0
      ;;
    not_git)
      echo "${repo_name}: ${repo_path} exists but is not a git checkout."
      managed_repo_remove_path "${repo_name}" "${repo_path}"

      managed_repo_clone "${repo_name}" "${repo_path}" "${repo_url}" "${repo_branch}"
      managed_repo_inspect "${repo_name}" "${repo_path}" "${repo_url}" "${repo_branch}"
      if [[ "${MANAGED_REPO_STATE}" != "up_to_date" && "${MANAGED_REPO_STATE}" != "fetch_timed_out" ]]; then
        echo "Managed repo clone verification failed for ${repo_name}."
        managed_repo_print_state
        return 1
      fi
      MANAGED_REPO_MESSAGE="Non-git managed path was removed; repo cloned at ${MANAGED_REPO_LOCAL_COMMIT}."
      managed_repo_human_summary
      managed_repo_print_state
      return 0
      ;;
    up_to_date|ahead_local|dirty|detached|diverged|branch_mismatch|remote_mismatch|unsafe_ownership|missing_origin|missing_remote_ref|fetch_failed|fetch_timed_out)
      managed_repo_human_summary
      managed_repo_print_state
      return 0
      ;;
    *)
      echo "Unhandled managed repo state for ${repo_name}: ${MANAGED_REPO_STATE}"
      managed_repo_print_state
      return 1
      ;;
  esac
}
