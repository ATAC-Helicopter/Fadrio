#ifndef FADRIO_NATIVE_H
#define FADRIO_NATIVE_H

#include <stddef.h>
#include <stdint.h>

#if defined(__cplusplus)
extern "C" {
#endif

#define VM_NATIVE_ABI_VERSION 1u

typedef struct vm_context vm_context;

typedef enum vm_event_type {
    VM_EVENT_READY = 1,
    VM_EVENT_DISCONNECTED = 2,
    VM_EVENT_SESSION_ADDED = 3,
    VM_EVENT_SESSION_CHANGED = 4,
    VM_EVENT_SESSION_REMOVED = 5
} vm_event_type;

typedef struct vm_session_info {
    uint32_t size;
    uint32_t node_id;
    int32_t process_id;
    float volume;
    uint8_t muted;
    uint8_t active;
    const char *application_name;
    const char *application_id;
    const char *application_icon_name;
    const char *process_binary;
    const char *media_name;
    const char *media_role;
} vm_session_info;

typedef struct vm_event {
    uint32_t size;
    vm_event_type type;
    uint64_t generation;
    vm_session_info session;
    const char *message;
} vm_event;

/* All strings and the event itself are immutable and valid only for the
 * duration of the callback. The receiver must copy any data it retains. */
typedef void (*vm_event_callback)(const vm_event *event, void *user_data);

uint32_t vm_get_abi_version(void);

/* The callback and user_data must remain valid until vm_context_destroy.
 * The returned opaque handle is owned by the caller and must be destroyed. */
vm_context *vm_context_create(vm_event_callback callback, void *user_data);
void vm_context_destroy(vm_context *context);

/* start owns a PipeWire thread until stop/destroy. Calls are idempotent. */
int vm_start(vm_context *context);
void vm_stop(vm_context *context);

/* Commands are serialized onto the PipeWire loop. A successful return includes
 * a PipeWire core round trip. Volume is normalized 0..1. */
int vm_set_stream_volume(vm_context *context, uint32_t node_id, float volume);
int vm_set_stream_mute(vm_context *context, uint32_t node_id, uint8_t muted);

#if defined(__cplusplus)
}
#endif

#endif
