/* Test-only ABI and finite native primitive fixture. No SDK/provider/product runtime body. */
#define _GNU_SOURCE 1
#include <stddef.h>
#include <stdint.h>
#include <stdio.h>
#include <string.h>
#include <spawn.h>
#include <signal.h>
#include <poll.h>
#include <fcntl.h>
#include <sys/stat.h>
#include <sys/wait.h>
#include <sys/pidfd.h>
#include <gnu/libc-version.h>
#include <dlfcn.h>

static int mismatch;
static int first_field = 1;
static void number(const char *name, size_t actual, size_t expected) {
    printf("%s\"%s\":{\"actual\":%zu,\"expected\":%zu,\"matches\":%s}",
           first_field ? "" : ",", name, actual, expected,
           actual == expected ? "true" : "false");
    first_field = 0;
    if (actual != expected) mismatch = 1;
}
static const char *const exports[] = {
    "pidfd_spawn", "pidfd_send_signal", "waitid", "poll", "pipe2",
    "posix_spawnattr_init", "posix_spawnattr_destroy",
    "posix_spawnattr_setflags", "posix_spawnattr_setsigmask",
    "posix_spawnattr_setsigdefault", "posix_spawn_file_actions_init",
    "posix_spawn_file_actions_destroy", "posix_spawn_file_actions_addfchdir_np",
    "posix_spawn_file_actions_adddup2", "posix_spawn_file_actions_addclose",
    "posix_spawn_file_actions_addclosefrom_np", "sigemptyset", "sigaddset",
    "openat", "mkdirat", "unlinkat", "renameat2", "statx", "fcntl",
    "read", "write", "close", "__errno_location"
};
static int abi_main(int argc, char **argv) {
    if (argc != 2 || strcmp(argv[1], "--abi-only") != 0) {
        fputs("only --abi-only is implemented; no child primitives admitted\n", stderr);
        return 64;
    }
#if !defined(__x86_64__) || !defined(__GLIBC__)
    fputs("unsupported ABI: Linux x64 glibc required\n", stderr);
    return 65;
#endif
    const char *version = gnu_get_libc_version();
    if (strcmp(version, "2.44") != 0) mismatch = 1;
    printf("{\"scope\":\"abi-only-no-children\",\"libcVersion\":\"%s\",\"expectedLibcVersion\":\"2.44\",\"layout\":{", version);
    number("pointerBytes", sizeof(void *), 8);
    number("spawnAttributesBytes", sizeof(posix_spawnattr_t), 336);
    number("spawnAttributesAlignment", _Alignof(posix_spawnattr_t), 8);
    number("spawnFileActionsBytes", sizeof(posix_spawn_file_actions_t), 80);
    number("spawnFileActionsAlignment", _Alignof(posix_spawn_file_actions_t), 8);
    number("signalSetBytes", sizeof(sigset_t), 128);
    number("signalInfoBytes", sizeof(siginfo_t), 128);
    number("signalInfoAlignment", _Alignof(siginfo_t), 8);
    number("signalInfoPidOffset", offsetof(siginfo_t, si_pid), 16);
    number("signalInfoStatusOffset", offsetof(siginfo_t, si_status), 24);
    number("pollDescriptorBytes", sizeof(struct pollfd), 8);
    number("pollDescriptorAlignment", _Alignof(struct pollfd), 4);
    number("pollEventsOffset", offsetof(struct pollfd, events), 4);
    number("pollReturnedEventsOffset", offsetof(struct pollfd, revents), 6);
    number("statxBytes", sizeof(struct statx), 256);
    number("statxAlignment", _Alignof(struct statx), 8);
    number("statxModeOffset", offsetof(struct statx, stx_mode), 28);
    number("statxInodeOffset", offsetof(struct statx, stx_ino), 32);
    number("statxDeviceMajorOffset", offsetof(struct statx, stx_dev_major), 136);
    number("statxDeviceMinorOffset", offsetof(struct statx, stx_dev_minor), 140);
    number("statxMountOffset", offsetof(struct statx, stx_mnt_id), 144);
    number("spawnSetsid", POSIX_SPAWN_SETSID, 128);
    number("spawnSignalDefault", POSIX_SPAWN_SETSIGDEF, 4);
    number("spawnSignalMask", POSIX_SPAWN_SETSIGMASK, 8);
    number("waitPidfd", P_PIDFD, 3);
    number("waitExited", WEXITED, 4);
    number("waitNoHang", WNOHANG, 1);
    number("waitNoWait", WNOWAIT, 16777216);
    number("pidfdSignalProcessGroup", PIDFD_SIGNAL_PROCESS_GROUP, 4);
    number("renameNoReplace", RENAME_NOREPLACE, 1);
    number("atSymlinkNoFollow", AT_SYMLINK_NOFOLLOW, 256);
    number("atEmptyPath", AT_EMPTY_PATH, 4096);
    number("openDirectory", O_DIRECTORY, 65536);
    number("openNoFollow", O_NOFOLLOW, 131072);
    number("openCloseOnExec", O_CLOEXEC, 524288);
    number("openPath", O_PATH, 2097152);
    printf("},\"exports\":{");
    for (size_t i = 0; i < sizeof(exports) / sizeof(exports[0]); ++i) {
        dlerror();
        void *address = dlsym(RTLD_DEFAULT, exports[i]);
        const char *error = dlerror();
        int present = address != NULL && error == NULL;
        printf("%s\"%s\":%s", i == 0 ? "" : ",", exports[i], present ? "true" : "false");
        if (!present) mismatch = 1;
    }
    printf("},\"matched\":%s}\n", mismatch ? "false" : "true");
    return mismatch ? 1 : 0;
}

/* Functional controls below are test-only. The original ABI mode stays unchanged. */
#include <errno.h>
#include <stdlib.h>
#include <unistd.h>
#include <time.h>
#include <dirent.h>
#include <math.h>
#include <limits.h>

static double work_end, cleanup_end;
static double original_work_epoch, original_cleanup_epoch;
static size_t direct_spawned, direct_reaped;
static void work_allowed(void);
static int active_pidfd = -1;
static int secondary_pidfd = -1;
static int cancel_fd = -1;
static const char *first_failure;
static int first_failure_errno;
static size_t controls;
static volatile sig_atomic_t group_term;
static void term_seen(int value) { (void)value; group_term = 1; }
static double monotonic_now(void) {
    struct timespec now;
    if (clock_gettime(CLOCK_MONOTONIC, &now) != 0) return 1e30;
    return (double)now.tv_sec + (double)now.tv_nsec / 1e9;
}
static void unknown_owner(const char *reason) {
    printf("{\"kind\":\"unknown-custody\",\"pidfd\":%d,\"reason\":\"%s\"}\n", active_pidfd, reason);
    fflush(stdout);
    /* No release, business, signal or cleanup authority remains. Keep the owner live. */
    for (;;) pause();
}
static int peek_exit(siginfo_t *info) {
    memset(info, 0, sizeof(*info));
    if (waitid(P_PIDFD, active_pidfd, info, WEXITED | WNOHANG | WNOWAIT) != 0) {
        unknown_owner("waitid-identity-unknown");
    }
    return info->si_pid != 0;
}
static void observe_session_before_reap(pid_t leader) {
    DIR *processes=opendir("/proc");if(processes==NULL)unknown_owner("session-census-unreadable");
    struct dirent *entry;int found=0;
    for (;;) {
        errno=0;entry=readdir(processes);
        if(entry==NULL){if(errno!=0)unknown_owner("session-census-iteration-unknown");break;}
        char *end;long pid=strtol(entry->d_name,&end,10);if(*end!=0 || pid<=0)continue;
        char name[100],text[4096];snprintf(name,sizeof(name),"/proc/%ld/stat",pid);
        int fd=open(name,O_RDONLY|O_CLOEXEC);
        if(fd<0){if(errno==ENOENT||errno==ESRCH)continue;unknown_owner("session-stat-unreadable");}
        ssize_t count=read(fd,text,sizeof(text)-1);int error=errno;close(fd);
        if(count<0){if(error==ENOENT||error==ESRCH)continue;unknown_owner("session-stat-read-unknown");}
        if(count==0 || count==(ssize_t)sizeof(text)-1)unknown_owner("session-stat-size-unknown");text[count]=0;
        char *closing=strrchr(text,')');long recorded_pid=strtol(text,&end,10);
        char state;long ppid,group,session;
        if(closing==NULL || recorded_pid!=pid || sscanf(closing+2,"%c %ld %ld %ld",&state,&ppid,&group,&session)!=4)unknown_owner("session-stat-parse-unknown");
        if(session==leader){
            if(pid!=leader || group!=leader || state!='Z')unknown_owner("session-member-not-settled");
            found=1;
        }
    }
    closedir(processes);if(!found)unknown_owner("unreaped-leader-not-observed");
    printf("{\"kind\":\"session-before-reap\",\"sessionId\":%d,\"onlyZombieLeader\":true}\n",leader);fflush(stdout);
}
static int signal_probe(const char *purpose) {
    if(strcmp(purpose,"cleanup")==0){if(monotonic_now()>=cleanup_end)unknown_owner("original-monotonic-cleanup-deadline");}
    else work_allowed();
    errno=0;int result=pidfd_send_signal(active_pidfd,SIGTERM,NULL,PIDFD_SIGNAL_PROCESS_GROUP);int error=result<0?errno:0;
    printf("{\"kind\":\"signal-result\",\"purpose\":\"%s\",\"pidfd\":%d,\"signal\":%d,\"flags\":%d,\"result\":%d,\"errno\":%d}\n",purpose,active_pidfd,SIGTERM,PIDFD_SIGNAL_PROCESS_GROUP,result,error);fflush(stdout);errno=error;return result;
}
static void finish_child(int expected_code, int expected_status) {
    siginfo_t info;
    int sent = 0;
    while (!peek_exit(&info)) {
        if (monotonic_now() >= work_end || first_failure != NULL) {
            if (first_failure == NULL) {first_failure = "original-work-deadline";first_failure_errno=ETIMEDOUT;}
            if (!sent && monotonic_now() < cleanup_end) {
                if (signal_probe("cleanup") != 0 && errno != ESRCH)
                    unknown_owner("pidfd-cleanup-signal-unknown");
                sent = 1;
            }
            if (monotonic_now() >= cleanup_end) unknown_owner("original-cleanup-deadline");
        }
        struct pollfd event = {active_pidfd, POLLIN, 0};
        double end = first_failure == NULL ? work_end : cleanup_end;
        int ms = (int)((end - monotonic_now()) * 1000.0);
        if (ms > 100) ms = 100;
        if (ms < 0) ms = 0;
        if (poll(&event, 1, ms) < 0 && errno != EINTR) unknown_owner("pidfd-poll-unknown");
    }
    printf("{\"kind\":\"child-exit-observed\",\"pid\":%d,\"code\":%d,\"status\":%d,\"reaperPid\":%d}\n", info.si_pid, info.si_code, info.si_status,getpid());
    fflush(stdout);
    if (first_failure == NULL && (info.si_code != expected_code || info.si_status != expected_status)) {first_failure = "unexpected-child-exit";first_failure_errno=0;}
    observe_session_before_reap(info.si_pid);
    /* This direct child is still unreaped here; its controlled session has no other members. */
    siginfo_t reaped;
    memset(&reaped, 0, sizeof(reaped));
    if (monotonic_now() >= cleanup_end) unknown_owner("original-cleanup-deadline-before-reap");
    if (waitid(P_PIDFD, active_pidfd, &reaped, WEXITED) != 0 || reaped.si_pid != info.si_pid)
        unknown_owner("pidfd-reap-unknown");
    direct_reaped++;close(active_pidfd); active_pidfd = -1;
    printf("{\"kind\":\"child-reaped\",\"pid\":%d,\"reaperPid\":%d}\n", info.si_pid,getpid()); fflush(stdout);
}
static void fail(const char *reason) {
    if (first_failure == NULL) {first_failure = reason;first_failure_errno=errno;}
    if (active_pidfd >= 0) finish_child(0, 0);
    if (secondary_pidfd >= 0) {active_pidfd=secondary_pidfd;secondary_pidfd=-1;finish_child(0,0);}
    printf("{\"kind\":\"failed\",\"firstFailure\":\"%s\",\"errno\":%d,\"checks\":%zu}\n", first_failure, first_failure_errno, controls);
    printf("{\"kind\":\"custody-terminal\",\"directSpawned\":%zu,\"directReaped\":%zu,\"activePidfds\":%d,\"passed\":false}\n",direct_spawned,direct_reaped,(active_pidfd>=0)+(secondary_pidfd>=0));
    fflush(stdout); exit(1);
}
static void work_allowed(void) {
    if (first_failure != NULL) fail(first_failure);
    if (monotonic_now() >= work_end) {errno=ETIMEDOUT;fail("original-work-deadline");}
    if (cancel_fd >= 0) {
        char byte;
        ssize_t read_result = read(cancel_fd, &byte, 1);
        if (read_result == 1 || read_result == 0) fail("outer-cancel-or-owner-lost");
        if (read_result < 0 && errno != EAGAIN && errno != EINTR) fail("outer-control-read");
    }
}
static void require(int condition, const char *name) {
    if (!condition) fail(name);
    work_allowed();
    controls++;
    printf("{\"kind\":\"check\",\"name\":\"%s\",\"passed\":true}\n", name); fflush(stdout);
}
static int identity_equal(int a, int b) {
    struct stat x, y;
    return fstat(a, &x) == 0 && fstat(b, &y) == 0 && x.st_dev == y.st_dev && x.st_ino == y.st_ino;
}
static int make_directory(int parent, const char *leaf) {
    work_allowed();
    require(mkdirat(parent, leaf, 0700) == 0, "mkdir-owned-directory");
    int fd = openat(parent, leaf, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC);
    require(fd >= 3, "open-owned-directory"); return fd;
}
static int open_directory(int parent, const char *leaf) {
    return openat(parent, leaf, O_RDONLY | O_DIRECTORY | O_NOFOLLOW | O_CLOEXEC);
}
static void write_file(int parent, const char *leaf, const char *value) {
    work_allowed();
    int fd = openat(parent, leaf, O_WRONLY | O_CREAT | O_EXCL | O_NOFOLLOW | O_CLOEXEC, 0600);
    require(fd >= 3, "create-exclusive-file");
    work_allowed();
    require(write(fd, value, strlen(value)) == (ssize_t)strlen(value), "write-owned-file"); close(fd);
}
static int pid_from_fdinfo(int fd) {
    char name[80], text[512];
    snprintf(name, sizeof(name), "/proc/self/fdinfo/%d", fd);
    int info = open(name, O_RDONLY | O_CLOEXEC);
    if (info < 0) unknown_owner("pidfd-info-unreadable");
    ssize_t count = read(info, text, sizeof(text)-1); close(info);
    if (count <= 0 || count == (ssize_t)sizeof(text)-1) unknown_owner("pidfd-info-malformed");
    text[count] = 0;
    char *row = strstr(text, "\nPid:\t");
    if (row == NULL) unknown_owner("pidfd-pid-unavailable");
    char *end;long parsed=strtol(row+6,&end,10);
    if(parsed<=0 || parsed>INT_MAX || *end!='\n')unknown_owner("pidfd-pid-malformed");
    int pid=(int)parsed;
    if (pid <= 0) unknown_owner("pidfd-pid-invalid");
    return pid;
}
static void launch_child(const char *self, int cwd, char *const argv[], int out, int err) {
    posix_spawnattr_t attributes;
    posix_spawn_file_actions_t actions;
    int e = posix_spawnattr_init(&attributes);
    require(e == 0, "spawn-attributes-init");
    e = posix_spawn_file_actions_init(&actions); require(e == 0, "spawn-actions-init");
    sigset_t empty, defaults; sigemptyset(&empty); sigemptyset(&defaults);
    sigaddset(&defaults, SIGTERM); sigaddset(&defaults, SIGINT); sigaddset(&defaults, SIGPIPE); sigaddset(&defaults,SIGCHLD);
    require(posix_spawnattr_setflags(&attributes, POSIX_SPAWN_SETSID | POSIX_SPAWN_SETSIGMASK | POSIX_SPAWN_SETSIGDEF) == 0, "spawn-session-signal-flags");
    require(posix_spawnattr_setsigmask(&attributes, &empty) == 0 && posix_spawnattr_setsigdefault(&attributes, &defaults) == 0, "spawn-explicit-signals");
    int input = open("/dev/null", O_RDONLY | O_CLOEXEC); require(input >= 3, "null-stdin-open");
    require(posix_spawn_file_actions_addfchdir_np(&actions, cwd) == 0, "fd-cwd-action");
    require(posix_spawn_file_actions_adddup2(&actions, input, 0) == 0 && posix_spawn_file_actions_adddup2(&actions, out, 1) == 0 && posix_spawn_file_actions_adddup2(&actions, err, 2) == 0, "explicit-stream-actions");
    require(posix_spawn_file_actions_addclosefrom_np(&actions, 3) == 0, "close-unrelated-descriptors-action");
    char *environment[] = {"EMPTY=", "RAW=space ; $(literal) ' quote = value", "LANG=C", NULL};
    active_pidfd = -1;
    /* The caller-held C owner and argv/env/actions remain live throughout synchronous startup. */
    work_allowed();
    e = pidfd_spawn(&active_pidfd, self, &actions, &attributes, argv, environment);
    posix_spawn_file_actions_destroy(&actions); posix_spawnattr_destroy(&attributes); close(input);
    if (e != 0) { active_pidfd = -1; errno = e; fail("pidfd-spawn-failure"); }
    if (active_pidfd < 0) unknown_owner("spawn-returned-no-pidfd");
    direct_spawned++;
    int pid = pid_from_fdinfo(active_pidfd);
    if (getpgid(pid) != pid || getsid(pid) != pid) unknown_owner("spawn-session-identity-unknown");
    printf("{\"kind\":\"child-owned\",\"pid\":%d,\"sessionId\":%d,\"pidfd\":%d,\"parentPid\":%d,\"mode\":\"%s\"}\n", pid, pid, active_pidfd,getpid(),argv[1]); fflush(stdout);
}
static int child_main(int argc, char **argv) {
    if (argc >= 2 && strcmp(argv[1], "--literal-child") == 0) {
        if (argc != 5 || strcmp(argv[2], "space ; $(literal) ' quote") != 0 || strcmp(argv[3], "") != 0) return 71;
        if (getenv("EMPTY") == NULL || strcmp(getenv("EMPTY"), "") != 0 || getenv("RAW") == NULL || strcmp(getenv("RAW"), "space ; $(literal) ' quote = value") != 0 || getenv("HOME") != NULL || getenv("PATH") != NULL) return 72;
        struct stat cwd; if (stat(".", &cwd) != 0) return 73;
        char identity[100]; snprintf(identity, sizeof(identity), "%llu:%llu", (unsigned long long)cwd.st_dev, (unsigned long long)cwd.st_ino);
        if (strcmp(identity, argv[4]) != 0) return 74;
        DIR *fds = opendir("/proc/self/fd"); if (fds == NULL) return 75;
        struct dirent *entry; int leaked = 0;
        for (;;) {errno=0;entry=readdir(fds);if(entry==NULL){if(errno!=0)leaked=1;break;}char *end;long fd=strtol(entry->d_name,&end,10);if(*end==0 && fd>=3 && fd!=dirfd(fds))leaked=1;}
        closedir(fds); if (leaked) return 75;
        if (write(1, "literal-ok\n", 11) != 11 || write(2, "literal-err\n", 12) != 12) return 76;
        return 0;
    }
    if (argc == 2 && strcmp(argv[1], "--writer-child") == 0) {
        if(write(2,"writer-ready\n",13)!=13)return 84;close(2);for(;;)pause();
    }
    if (argc == 2 && strcmp(argv[1], "--flood-child") == 0) {
        char output[2048];memset(output,'x',sizeof(output));return write(1,output,sizeof(output))==(ssize_t)sizeof(output)?0:85;
    }
    if (argc == 2 && strcmp(argv[1], "--group-child") == 0) {
        sigset_t blocked, empty; sigemptyset(&blocked);sigaddset(&blocked,SIGTERM);sigemptyset(&empty);
        struct sigaction action; memset(&action,0,sizeof(action)); action.sa_handler=term_seen; sigemptyset(&action.sa_mask);
        if (sigprocmask(SIG_BLOCK,&blocked,NULL)!=0 || sigaction(SIGTERM,&action,NULL)!=0) return 77;
        int readiness[2];if(pipe2(readiness,O_CLOEXEC)!=0)return 78;
        pid_t grandchild = fork(); if (grandchild < 0) {close(readiness[0]);close(readiness[1]);return 78;}
        if (grandchild == 0) {
            close(readiness[0]);
            if(signal(SIGTERM,SIG_DFL)==SIG_ERR || sigprocmask(SIG_SETMASK,&empty,NULL)!=0 || write(readiness[1],"r",1)!=1)_exit(82);
            close(readiness[1]);for (;;) pause();
        }
        close(readiness[1]);char ready;ssize_t n;
        do {n=read(readiness[0],&ready,1);} while(n<0 && errno==EINTR);close(readiness[0]);
        if(n!=1 || ready!='r') {int status;while(waitpid(grandchild,&status,0)<0)if(errno!=EINTR)for(;;)pause();return 83;}
        int report_ok=write(1,"group-ready\n",12)==12;
        while (!group_term) sigsuspend(&empty);
        int status; while (waitpid(grandchild,&status,0) < 0) if (errno != EINTR) for(;;)pause();
        if(!report_ok || !WIFSIGNALED(status) || WTERMSIG(status)!=SIGTERM)return 81;
        char result[80];int size=snprintf(result,sizeof(result),"writer-reaped:%d\n",grandchild);return write(1,result,(size_t)size)==size?0:86;
    }
    return 64;
}
static void read_stream(int fd, char *value, size_t size) {
    size_t count=0;
    for (;;) {
        work_allowed();
        struct pollfd pollfd = {fd, POLLIN | POLLHUP, 0};
        if (poll(&pollfd,1,100) < 0) { if (errno==EINTR) continue; fail("stream-poll"); }
        if (!(pollfd.revents & (POLLIN | POLLHUP))) continue;
        ssize_t bytes=read(fd,value+count,size-count-1);
        if (bytes < 0) { if (errno==EINTR || errno==EAGAIN) continue; fail("stream-read"); }
        if (bytes == 0) { value[count]=0; return; }
        count+=(size_t)bytes; if (count >= size-1) {errno=EOVERFLOW;fail("stream-output-cap");}
    }
}
static void process_controls(int root, const char *self) {
    int cwd=make_directory(root,"cwd");
    struct stat identity; require(fstat(cwd,&identity)==0,"capture-cwd-identity");
    work_allowed();
    require(renameat(root,"cwd",root,"cwd-moved")==0,"rename-cwd-before-spawn");
    int replacement=make_directory(root,"cwd"); require(!identity_equal(cwd,replacement),"replacement-cwd-distinct"); close(replacement);
    int out[2],err[2]; require(pipe2(out,O_CLOEXEC|O_NONBLOCK)==0 && pipe2(err,O_CLOEXEC|O_NONBLOCK)==0,"separate-owned-pipes");
    char expected[100]; snprintf(expected,sizeof(expected),"%llu:%llu",(unsigned long long)identity.st_dev,(unsigned long long)identity.st_ino);
    char *argv[]={(char *)self,"--literal-child","space ; $(literal) ' quote","",expected,NULL};
    int sentinel=fcntl(cwd,F_DUPFD,200);require(sentinel>=200,"non-cloexec-high-fd-sentinel");
    launch_child(self,cwd,argv,out[1],err[1]);close(sentinel); close(out[1]);close(err[1]);
    char stdout_value[256],stderr_value[256]; read_stream(out[0],stdout_value,sizeof(stdout_value));read_stream(err[0],stderr_value,sizeof(stderr_value));close(out[0]);close(err[0]);
    finish_child(CLD_EXITED,0);
    require(strcmp(stdout_value,"literal-ok\n")==0 && strcmp(stderr_value,"literal-err\n")==0,"literal-argv-env-fd-cwd-streams-and-closed-inheritance");
    require(pipe2(out,O_CLOEXEC|O_NONBLOCK)==0 && pipe2(err,O_CLOEXEC|O_NONBLOCK)==0,"group-owned-pipes");
    char *group_argv[]={(char *)self,"--group-child",NULL};launch_child(self,cwd,group_argv,out[1],err[1]);int group_leader=pid_from_fdinfo(active_pidfd);close(out[1]);close(err[1]);
    /* Read finite readiness without waiting for EOF held by the child family. */
    size_t ready=0; char line[32];
    while (ready < 12) {
        require(monotonic_now() < work_end,"group-readiness-deadline");
        struct pollfd event={out[0],POLLIN,0}; int p=poll(&event,1,100);
        if (p<0 && errno!=EINTR) fail("group-ready-poll");
        if (p>0 && (event.revents&POLLIN)) { ssize_t n=read(out[0],line+ready,12-ready); if(n<=0)fail("group-ready-read");ready+=(size_t)n; }
    }
    require(memcmp(line,"group-ready\n",12)==0,"controlled-family-ready");
    require(signal_probe("controlled-group")==0,"pidfd-safe-group-signal-request");
    read_stream(out[0],stdout_value,sizeof(stdout_value));read_stream(err[0],stderr_value,sizeof(stderr_value));close(out[0]);close(err[0]);finish_child(CLD_EXITED,0);
    int reaped_writer;char trailing;require(sscanf(stdout_value,"writer-reaped:%d%c",&reaped_writer,&trailing)==2 && reaped_writer>0 && trailing=='\n' && stderr_value[0]==0,"group-leader-waitpid-reaped-writer-and-pipes-eof");
    printf("{\"kind\":\"grandchild-reaped\",\"pid\":%d,\"parentPid\":%d,\"reaper\":\"group-leader-waitpid\"}\n",reaped_writer,group_leader);fflush(stdout);close(cwd);
}
static void direct_exit_before_pipe_control(int root,const char *self) {
    int out[2],err[2],ready[2];
    require(pipe2(out,O_CLOEXEC|O_NONBLOCK)==0 && pipe2(err,O_CLOEXEC|O_NONBLOCK)==0 && pipe2(ready,O_CLOEXEC|O_NONBLOCK)==0,"sibling-owned-stream-pipes");
    struct stat cwd;require(fstat(root,&cwd)==0,"sibling-cwd-identity");char identity[100];
    snprintf(identity,sizeof(identity),"%llu:%llu",(unsigned long long)cwd.st_dev,(unsigned long long)cwd.st_ino);
    char *leader[]={(char *)self,"--literal-child","space ; $(literal) ' quote","",identity,NULL};
    launch_child(self,root,leader,out[1],err[1]);secondary_pidfd=active_pidfd;active_pidfd=-1;
    char *writer[]={(char *)self,"--writer-child",NULL};launch_child(self,root,writer,out[1],ready[1]);
    /* Both actual pidfds are now held by this direct parent; neither PID is reacquired. */
    int writer_pidfd=active_pidfd;active_pidfd=secondary_pidfd;secondary_pidfd=writer_pidfd;
    close(out[1]);close(err[1]);close(ready[1]);
    require(fcntl(out[1],F_GETFD)==-1 && errno==EBADF,"fixture-parent-shared-writer-closed");
    char text[256];read_stream(ready[0],text,sizeof(text));close(ready[0]);
    require(strcmp(text,"writer-ready\n")==0,"direct-sibling-writer-ready");
    finish_child(CLD_EXITED,0);
    require(active_pidfd==-1 && secondary_pidfd>=0,"direct-leader-reaped-while-sibling-owned");
    /* Read exactly the leader payload; the sibling alone still owns the write side. */
    size_t count=0;while(count<11){work_allowed();ssize_t n=read(out[0],text+count,11-count);if(n>0)count+=(size_t)n;else if(n<0 && (errno==EAGAIN||errno==EINTR)){struct pollfd p={out[0],POLLIN,0};poll(&p,1,100);}else fail("leader-payload-read");}
    require(memcmp(text,"literal-ok\n",11)==0,"exited-leader-output-observed");
    char byte;ssize_t n=read(out[0],&byte,1);
    require(n==-1 && errno==EAGAIN,"actual-direct-exit-before-held-pipe-eof");
    read_stream(err[0],text,sizeof(text));close(err[0]);require(strcmp(text,"literal-err\n")==0,"direct-leader-stderr-eof");
    active_pidfd=secondary_pidfd;secondary_pidfd=-1;
    require(signal_probe("direct-writer")==0,"exact-direct-writer-pidfd-signal");
    finish_child(CLD_KILLED,SIGTERM);read_stream(out[0],text,sizeof(text));close(out[0]);
    require(text[0]==0 && active_pidfd==-1,"direct-writer-reaped-then-actual-pipe-eof");
}
static void negative_process_control(int root,const char *self,int flood) {
    int out[2],err[2];require(pipe2(out,O_CLOEXEC|O_NONBLOCK)==0 && pipe2(err,O_CLOEXEC|O_NONBLOCK)==0,"negative-control-owned-pipes");
    char *argv[]={(char *)self,flood?"--flood-child":"--writer-child",NULL};launch_child(self,root,argv,out[1],err[1]);close(out[1]);close(err[1]);
    char text[256];if(!flood){read_stream(err[0],text,sizeof(text));require(strcmp(text,"writer-ready\n")==0,"negative-deadline-writer-ready");}
    read_stream(out[0],text,sizeof(text));
    fail("negative-control-unexpected-success");
}
static int no_replace_probe(int source,const char *leaf,int parent,const char *target,const char *case_name) {
    work_allowed();
    errno=0;int result=renameat2(source,leaf,parent,target,RENAME_NOREPLACE);int error=result<0?errno:0;
    printf("{\"kind\":\"rename-result\",\"case\":\"%s\",\"result\":%d,\"errno\":%d}\n",case_name,result,error);fflush(stdout);errno=error;return result;
}
static int commit_report_probe(int parent,int inject_loss) {
    int destination=open_directory(parent,"report-loss-target");if(destination<0)return -1;close(destination);
    if(inject_loss){errno=EIO;return -1;}return 0;
}
static void filesystem_controls(int root) {
    int parent=make_directory(root,"parent");int stage=make_directory(parent,"workspace");write_file(stage,"payload","original");
    require(no_replace_probe(parent,"workspace",parent,"target","absent-target")==0,"no-replace-absent-target-success");
    int target=open_directory(parent,"target");require(target>=3 && identity_equal(stage,target),"committed-object-identity");close(target);close(stage);
    stage=make_directory(parent,"workspace");write_file(stage,"payload","second");
    errno=0;int rc=no_replace_probe(parent,"workspace",parent,"target","appearing-target");require(rc==-1 && errno==EEXIST,"appearing-target-eexist-preserved");
    target=open_directory(parent,"target");struct stat payload;require(target>=3 && fstatat(target,"payload",&payload,AT_SYMLINK_NOFOLLOW)==0 && payload.st_size==8,"existing-target-content-preserved");close(target);
    work_allowed();
    require(symlinkat("target",parent,"linked-target")==0,"create-fixture-target-link");errno=0;rc=no_replace_probe(parent,"workspace",parent,"linked-target","target-link");require(rc==-1 && errno==EEXIST,"target-link-not-replaced");
    int link=open_directory(parent,"linked-target");require(link==-1 && (errno==ELOOP||errno==ENOTDIR),"directory-link-refused");
    work_allowed();
    require(renameat(root,"parent",root,"parent-held")==0,"relocate-parent-control");int foreign=make_directory(root,"parent");
    require(!identity_equal(parent,foreign),"changed-parent-binding-refused-before-commit");
    struct stat untouched;require(fstatat(foreign,"target",&untouched,AT_SYMLINK_NOFOLLOW)==-1 && errno==ENOENT,"replacement-parent-untouched");
    require(no_replace_probe(parent,"workspace",parent,"late-target","parent-relocated")==0,"late-parent-relocation-commit-bound-held-object");
    target=open_directory(parent,"late-target");require(target>=3 && identity_equal(stage,target),"actual-late-commit-object-retained");close(target);
    require(fstatat(foreign,"late-target",&untouched,AT_SYMLINK_NOFOLLOW)==-1 && errno==ENOENT,"requested-path-commit-unknown-no-foreign-publication");
    close(stage);stage=make_directory(parent,"workspace");
    work_allowed();
    require(renameat(parent,"workspace",parent,"stage-held")==0,"relocate-stage-control");int substituted=make_directory(parent,"workspace");
    require(!identity_equal(stage,substituted),"changed-staging-binding-refused");close(substituted);close(stage);
    /* Inject report loss after a successful syscall: retain actual destination, never repeat. */
    require(no_replace_probe(parent,"workspace",parent,"report-loss-target","report-loss")==0,"post-rename-report-loss-actual-commit-retained");
    errno=0;int report=commit_report_probe(parent,1);int report_error=errno;require(report==-1 && report_error==EIO,"injected-post-commit-report-probe-failure");
    printf("{\"kind\":\"commit-observed\",\"syscallResult\":0,\"reportResult\":%d,\"reportErrno\":%d,\"logicalStatus\":\"Unknown\"}\n",report,report_error);fflush(stdout);
    require(fstatat(parent,"workspace",&untouched,AT_SYMLINK_NOFOLLOW)==-1 && errno==ENOENT && fstatat(parent,"report-loss-target",&untouched,AT_SYMLINK_NOFOLLOW)==0,"unknown-report-no-retry-or-rollback");
    close(foreign);close(parent);
}
int main(int argc,char **argv) {
    if (argc==2 && strcmp(argv[1],"--abi-only")==0) return abi_main(argc,argv);
    if (argc>=2 && (strcmp(argv[1],"--literal-child")==0 || strcmp(argv[1],"--group-child")==0 || strcmp(argv[1],"--writer-child")==0 || strcmp(argv[1],"--flood-child")==0)) return child_main(argc,argv);
    if (argc!=7 || (strcmp(argv[1],"--primitives")!=0 && strcmp(argv[1],"--timeout-control")!=0 && strcmp(argv[1],"--output-control")!=0)) { fputs("fixed --abi-only or --primitives ROOT WORK_EPOCH CLEANUP_EPOCH WORK_MONOTONIC CLEANUP_MONOTONIC only\n",stderr);return 64; }
    char *end;original_work_epoch=strtod(argv[3],&end);if(*end || !isfinite(original_work_epoch))return 65;
    original_cleanup_epoch=strtod(argv[4],&end);if(*end || !isfinite(original_cleanup_epoch))return 65;
    double admitted_work_monotonic=strtod(argv[5],&end);if(*end || !isfinite(admitted_work_monotonic))return 65;
    double admitted_cleanup_monotonic=strtod(argv[6],&end);if(*end || !isfinite(admitted_cleanup_monotonic))return 65;
    struct timespec real_anchor,mono_anchor;
    if(clock_gettime(CLOCK_REALTIME,&real_anchor)!=0 || clock_gettime(CLOCK_MONOTONIC,&mono_anchor)!=0)return 65;
    double epoch=(double)real_anchor.tv_sec+(double)real_anchor.tv_nsec/1e9;
    double mono=(double)mono_anchor.tv_sec+(double)mono_anchor.tv_nsec/1e9;
    work_end=mono+original_work_epoch-epoch;if(work_end>admitted_work_monotonic)work_end=admitted_work_monotonic;
    cleanup_end=mono+original_cleanup_epoch-epoch;if(cleanup_end>admitted_cleanup_monotonic)cleanup_end=admitted_cleanup_monotonic;
    if(work_end<=monotonic_now() || cleanup_end<work_end)return 65;
    printf("{\"kind\":\"clock-anchor\",\"originalWorkEpoch\":%.9f,\"originalCleanupEpoch\":%.9f,\"effectiveWorkMonotonic\":%.9f,\"effectiveCleanupMonotonic\":%.9f}\n",original_work_epoch,original_cleanup_epoch,work_end,cleanup_end);fflush(stdout);
    cancel_fd=0;if(fcntl(cancel_fd,F_SETFL,O_NONBLOCK)<0)return 65;
    int root=open(argv[2],O_RDONLY|O_DIRECTORY|O_NOFOLLOW|O_CLOEXEC);require(root>=3,"open-owned-fixture-root");
    if(strcmp(argv[1],"--timeout-control")==0 || strcmp(argv[1],"--output-control")==0)negative_process_control(root,argv[0],strcmp(argv[1],"--output-control")==0);
    filesystem_controls(root);process_controls(root,argv[0]);direct_exit_before_pipe_control(root,argv[0]);close(root);
    require(active_pidfd==-1 && secondary_pidfd==-1,"all-owned-children-reaped");
    printf("{\"kind\":\"custody-terminal\",\"directSpawned\":%zu,\"directReaped\":%zu,\"activePidfds\":%d,\"passed\":true}\n",direct_spawned,direct_reaped,(active_pidfd>=0)+(secondary_pidfd>=0));
    printf("{\"kind\":\"complete\",\"scope\":\"native-primitives-only\",\"checks\":%zu,\"passed\":true}\n",controls);fflush(stdout);return 0;
}
