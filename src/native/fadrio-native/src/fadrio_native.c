#define _POSIX_C_SOURCE 200809L
#include "fadrio_native.h"

#include <errno.h>
#include <pipewire/pipewire.h>
#include <spa/param/props.h>
#include <spa/pod/builder.h>
#include <spa/pod/parser.h>
#include <stdbool.h>
#include <stdlib.h>
#include <string.h>

typedef struct vm_node {
    struct vm_node *next;
    struct vm_context *owner;
    uint32_t id;
    struct pw_node *proxy;
    struct spa_hook listener;
    char *application_name;
    char *application_id;
    char *application_icon_name;
    char *process_binary;
    char *media_name;
    char *media_role;
    int32_t process_id;
    float volume;
    bool muted;
    bool active;
} vm_node;

struct vm_context {
    vm_event_callback callback;
    void *user_data;
    struct pw_thread_loop *loop;
    struct pw_context *pw_context;
    struct pw_core *core;
    struct pw_registry *registry;
    struct spa_hook core_listener;
    struct spa_hook registry_listener;
    vm_node *nodes;
    uint64_t generation;
    int sync_sequence;
    int command_sequence;
    int command_result;
    bool command_pending;
    bool command_completed;
    bool started;
    bool ready;
};

static char *vm_copy_property(const struct spa_dict *properties, const char *key)
{
    const char *value = properties == NULL ? NULL : spa_dict_lookup(properties, key);
    return value == NULL ? NULL : strdup(value);
}

static void vm_replace_property(char **target, const struct spa_dict *properties, const char *key)
{
    char *replacement = vm_copy_property(properties, key);
    free(*target);
    *target = replacement;
}

static int32_t vm_process_id(const struct spa_dict *properties)
{
    const char *value = properties == NULL ? NULL : spa_dict_lookup(properties, PW_KEY_APP_PROCESS_ID);
    if (value == NULL) {
        return -1;
    }

    char *end = NULL;
    long result = strtol(value, &end, 10);
    return end == value || *end != '\0' || result <= 0 || result > INT32_MAX ? -1 : (int32_t)result;
}

static void vm_emit_node(vm_node *node, vm_event_type type)
{
    vm_context *context = node->owner;
    if (context->callback == NULL) {
        return;
    }

    vm_event event = {
        .size = sizeof(vm_event),
        .type = type,
        .generation = context->generation,
        .session = {
            .size = sizeof(vm_session_info),
            .node_id = node->id,
            .process_id = node->process_id,
            .volume = node->volume,
            .muted = node->muted,
            .active = node->active,
            .application_name = node->application_name,
            .application_id = node->application_id,
            .application_icon_name = node->application_icon_name,
            .process_binary = node->process_binary,
            .media_name = node->media_name,
            .media_role = node->media_role
        },
        .message = NULL
    };
    context->callback(&event, context->user_data);
}

static void vm_emit_simple(vm_context *context, vm_event_type type, const char *message)
{
    if (context->callback == NULL) {
        return;
    }

    vm_event event = {
        .size = sizeof(vm_event),
        .type = type,
        .generation = context->generation,
        .session = {.size = sizeof(vm_session_info)},
        .message = message
    };
    context->callback(&event, context->user_data);
}

static void vm_update_node_properties(vm_node *node, const struct spa_dict *properties)
{
    if (properties == NULL) {
        return;
    }

    vm_replace_property(&node->application_name, properties, PW_KEY_APP_NAME);
    vm_replace_property(&node->application_id, properties, PW_KEY_APP_ID);
    vm_replace_property(&node->application_icon_name, properties, PW_KEY_APP_ICON_NAME);
    vm_replace_property(&node->process_binary, properties, PW_KEY_APP_PROCESS_BINARY);
    vm_replace_property(&node->media_name, properties, PW_KEY_MEDIA_NAME);
    vm_replace_property(&node->media_role, properties, PW_KEY_MEDIA_ROLE);
    node->process_id = vm_process_id(properties);
}

static void vm_node_info(void *data, const struct pw_node_info *info)
{
    vm_node *node = data;
    if (info->props != NULL) {
        vm_update_node_properties(node, info->props);
        vm_emit_node(node, VM_EVENT_SESSION_CHANGED);
    }
    if ((info->change_mask & PW_NODE_CHANGE_MASK_PARAMS) != 0) {
        for (uint32_t index = 0; index < info->n_params; index++) {
            if (info->params[index].id == SPA_PARAM_Props) {
                pw_node_enum_params(node->proxy, 0, SPA_PARAM_Props, 0, UINT32_MAX, NULL);
                break;
            }
        }
    }
}

static void vm_node_param(void *data, int sequence, uint32_t id, uint32_t index,
                          uint32_t next, const struct spa_pod *parameter)
{
    (void)sequence;
    (void)index;
    (void)next;
    vm_node *node = data;
    if (id != SPA_PARAM_Props || parameter == NULL) {
        return;
    }
    float volume = node->volume;
    bool muted = node->muted;
    if (spa_pod_parse_object(parameter, SPA_TYPE_OBJECT_Props, NULL,
                             SPA_PROP_volume, SPA_POD_OPT_Float(&volume),
                             SPA_PROP_mute, SPA_POD_OPT_Bool(&muted)) >= 0) {
        node->volume = volume;
        node->muted = muted;
        vm_emit_node(node, VM_EVENT_SESSION_CHANGED);
    }
}

static const struct pw_node_events vm_node_events = {
    PW_VERSION_NODE_EVENTS,
    .info = vm_node_info,
    .param = vm_node_param
};

static void vm_free_node(vm_node *node)
{
    if (node->proxy != NULL) {
        spa_hook_remove(&node->listener);
        pw_proxy_destroy((struct pw_proxy *)node->proxy);
    }
    free(node->application_name);
    free(node->application_id);
    free(node->application_icon_name);
    free(node->process_binary);
    free(node->media_name);
    free(node->media_role);
    free(node);
}

static void vm_registry_global(void *data, uint32_t id, uint32_t permissions,
                               const char *type, uint32_t version,
                               const struct spa_dict *properties)
{
    (void)permissions;
    (void)version;
    vm_context *context = data;
    const char *media_class = properties == NULL ? NULL : spa_dict_lookup(properties, PW_KEY_MEDIA_CLASS);
    if (strcmp(type, PW_TYPE_INTERFACE_Node) != 0 || media_class == NULL ||
        strcmp(media_class, "Stream/Output/Audio") != 0) {
        return;
    }

    vm_node *node = calloc(1, sizeof(*node));
    if (node == NULL) {
        return;
    }
    node->owner = context;
    node->id = id;
    node->process_id = -1;
    node->volume = 1.0F;
    node->active = true;
    vm_update_node_properties(node, properties);
    node->proxy = pw_registry_bind(context->registry, id, PW_TYPE_INTERFACE_Node,
                                   PW_VERSION_NODE, 0);
    if (node->proxy != NULL) {
        pw_node_add_listener(node->proxy, &node->listener, &vm_node_events, node);
    }
    node->next = context->nodes;
    context->nodes = node;
    vm_emit_node(node, VM_EVENT_SESSION_ADDED);
}

static void vm_registry_global_remove(void *data, uint32_t id)
{
    vm_context *context = data;
    vm_node **cursor = &context->nodes;
    while (*cursor != NULL) {
        vm_node *node = *cursor;
        if (node->id == id) {
            vm_emit_node(node, VM_EVENT_SESSION_REMOVED);
            *cursor = node->next;
            vm_free_node(node);
            return;
        }
        cursor = &node->next;
    }
}

static const struct pw_registry_events vm_registry_events = {
    PW_VERSION_REGISTRY_EVENTS,
    .global = vm_registry_global,
    .global_remove = vm_registry_global_remove
};

static void vm_core_done(void *data, uint32_t id, int sequence)
{
    vm_context *context = data;
    if (id != PW_ID_CORE) {
        return;
    }

    if (sequence == context->sync_sequence && !context->ready) {
        context->ready = true;
        vm_emit_simple(context, VM_EVENT_READY, NULL);
    }
    if (context->command_pending && sequence == context->command_sequence) {
        context->command_result = 0;
        context->command_completed = true;
    }
    pw_thread_loop_signal(context->loop, false);
}

static void vm_core_error(void *data, uint32_t id, int sequence, int result, const char *message)
{
    (void)id;
    (void)sequence;
    vm_context *context = data;
    if (result == -EPIPE) {
        context->ready = false;
        context->command_result = result;
        context->command_completed = true;
        vm_emit_simple(context, VM_EVENT_DISCONNECTED, message);
        pw_thread_loop_signal(context->loop, false);
    }
}

static const struct pw_core_events vm_core_events = {
    PW_VERSION_CORE_EVENTS,
    .done = vm_core_done,
    .error = vm_core_error
};

uint32_t vm_get_abi_version(void)
{
    return VM_NATIVE_ABI_VERSION;
}

vm_context *vm_context_create(vm_event_callback callback, void *user_data)
{
    vm_context *context = calloc(1, sizeof(*context));
    if (context != NULL) {
        context->callback = callback;
        context->user_data = user_data;
    }
    return context;
}

void vm_stop(vm_context *context)
{
    if (context == NULL || !context->started) {
        return;
    }

    pw_thread_loop_lock(context->loop);
    while (context->nodes != NULL) {
        vm_node *node = context->nodes;
        context->nodes = node->next;
        vm_free_node(node);
    }
    if (context->registry != NULL) {
        spa_hook_remove(&context->registry_listener);
        pw_proxy_destroy((struct pw_proxy *)context->registry);
        context->registry = NULL;
    }
    if (context->core != NULL) {
        spa_hook_remove(&context->core_listener);
        pw_core_disconnect(context->core);
        context->core = NULL;
    }
    if (context->pw_context != NULL) {
        pw_context_destroy(context->pw_context);
        context->pw_context = NULL;
    }
    pw_thread_loop_unlock(context->loop);
    pw_thread_loop_stop(context->loop);
    pw_thread_loop_destroy(context->loop);
    context->loop = NULL;
    context->started = false;
    context->ready = false;
}

void vm_context_destroy(vm_context *context)
{
    if (context == NULL) {
        return;
    }
    vm_stop(context);
    free(context);
}

int vm_start(vm_context *context)
{
    if (context == NULL) {
        return -EINVAL;
    }
    if (context->started) {
        return 0;
    }

    pw_init(NULL, NULL);
    context->loop = pw_thread_loop_new("fadrio-pipewire", NULL);
    if (context->loop == NULL) {
        return -ENOMEM;
    }
    context->pw_context = pw_context_new(pw_thread_loop_get_loop(context->loop), NULL, 0);
    if (context->pw_context == NULL) {
        pw_thread_loop_destroy(context->loop);
        context->loop = NULL;
        return -ENOMEM;
    }
    if (pw_thread_loop_start(context->loop) < 0) {
        pw_context_destroy(context->pw_context);
        pw_thread_loop_destroy(context->loop);
        context->pw_context = NULL;
        context->loop = NULL;
        return -ECONNREFUSED;
    }
    context->started = true;
    context->generation++;
    context->command_pending = false;
    context->command_completed = false;

    pw_thread_loop_lock(context->loop);
    context->core = pw_context_connect(context->pw_context, NULL, 0);
    if (context->core == NULL) {
        pw_thread_loop_unlock(context->loop);
        vm_stop(context);
        return -ECONNREFUSED;
    }
    pw_core_add_listener(context->core, &context->core_listener, &vm_core_events, context);
    context->registry = pw_core_get_registry(context->core, PW_VERSION_REGISTRY, 0);
    if (context->registry == NULL) {
        pw_thread_loop_unlock(context->loop);
        vm_stop(context);
        return -EIO;
    }
    pw_registry_add_listener(context->registry, &context->registry_listener, &vm_registry_events, context);
    context->sync_sequence = pw_core_sync(context->core, PW_ID_CORE, 0);
    pw_thread_loop_unlock(context->loop);
    return 0;
}

static vm_node *vm_find_node(vm_context *context, uint32_t node_id)
{
    for (vm_node *node = context->nodes; node != NULL; node = node->next) {
        if (node->id == node_id) {
            return node;
        }
    }
    return NULL;
}

static int vm_roundtrip_locked(vm_context *context)
{
    while (context->ready && context->command_pending) {
        pw_thread_loop_wait(context->loop);
    }
    if (context->core == NULL || !context->ready) {
        return -ENOTCONN;
    }

    int sequence = pw_core_sync(context->core, PW_ID_CORE, 0);
    if (sequence < 0) {
        return sequence;
    }

    context->command_sequence = sequence;
    context->command_result = -EINPROGRESS;
    context->command_pending = true;
    context->command_completed = false;
    while (context->ready && !context->command_completed) {
        pw_thread_loop_wait(context->loop);
    }
    int result = context->command_completed ? context->command_result : -EPIPE;
    context->command_pending = false;
    pw_thread_loop_signal(context->loop, false);
    return result;
}

int vm_set_stream_volume(vm_context *context, uint32_t node_id, float volume)
{
    if (context == NULL || !context->started || volume < 0.0F || volume > 1.0F) {
        return -EINVAL;
    }
    pw_thread_loop_lock(context->loop);
    vm_node *node = vm_find_node(context, node_id);
    if (node == NULL || node->proxy == NULL) {
        pw_thread_loop_unlock(context->loop);
        return -ENOENT;
    }
    uint8_t buffer[256];
    struct spa_pod_builder builder = SPA_POD_BUILDER_INIT(buffer, sizeof(buffer));
    const struct spa_pod *parameter = spa_pod_builder_add_object(
        &builder, SPA_TYPE_OBJECT_Props, SPA_PARAM_Props,
        SPA_PROP_volume, SPA_POD_Float(volume));
    int result = pw_node_set_param(node->proxy, SPA_PARAM_Props, 0, parameter);
    if (result >= 0) {
        result = vm_roundtrip_locked(context);
    }
    pw_thread_loop_unlock(context->loop);
    return result;
}

int vm_set_stream_mute(vm_context *context, uint32_t node_id, uint8_t muted)
{
    if (context == NULL || !context->started) {
        return -EINVAL;
    }
    pw_thread_loop_lock(context->loop);
    vm_node *node = vm_find_node(context, node_id);
    if (node == NULL || node->proxy == NULL) {
        pw_thread_loop_unlock(context->loop);
        return -ENOENT;
    }
    uint8_t buffer[256];
    struct spa_pod_builder builder = SPA_POD_BUILDER_INIT(buffer, sizeof(buffer));
    const struct spa_pod *parameter = spa_pod_builder_add_object(
        &builder, SPA_TYPE_OBJECT_Props, SPA_PARAM_Props,
        SPA_PROP_mute, SPA_POD_Bool(muted != 0));
    int result = pw_node_set_param(node->proxy, SPA_PARAM_Props, 0, parameter);
    if (result >= 0) {
        result = vm_roundtrip_locked(context);
    }
    pw_thread_loop_unlock(context->loop);
    return result;
}
