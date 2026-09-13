#include "fadrio_native.h"

#include <assert.h>

static void ignore_event(const vm_event *event, void *user_data)
{
    (void)event;
    (void)user_data;
}

int main(void)
{
    assert(vm_get_abi_version() == VM_NATIVE_ABI_VERSION);
    for (int index = 0; index < 1000; index++) {
        vm_context *context = vm_context_create(ignore_event, NULL);
        assert(context != NULL);
        vm_stop(context);
        vm_context_destroy(context);
    }
    vm_context_destroy(NULL);
    return 0;
}
