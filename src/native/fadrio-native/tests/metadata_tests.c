/* Exercise private callback metadata handling without exporting native internals. */
#include "../src/fadrio_native.c"
#include <assert.h>

int main(void)
{
    vm_node node = {.process_id = -1};
    const struct spa_dict_item initial_items[] = {
        {PW_KEY_APP_NAME, "Fixture"},
        {PW_KEY_APP_ID, "dev.fglabs.fixture"},
        {PW_KEY_APP_PROCESS_ID, "123"},
        {PW_KEY_APP_ICON_NAME, "fixture-icon"},
        {PW_KEY_APP_PROCESS_BINARY, "fixture-bin"}
    };
    const struct spa_dict initial = SPA_DICT_INIT_ARRAY(initial_items);
    vm_update_node_properties(&node, &initial);

    const struct spa_dict_item delta_items[] = {{PW_KEY_MEDIA_NAME, "New stream title"}};
    const struct spa_dict delta = SPA_DICT_INIT_ARRAY(delta_items);
    vm_update_node_properties(&node, &delta);
    vm_update_node_properties(&node, NULL);
    assert(strcmp(node.application_name, "Fixture") == 0);
    assert(strcmp(node.application_id, "dev.fglabs.fixture") == 0);
    assert(strcmp(node.application_icon_name, "fixture-icon") == 0);
    assert(strcmp(node.process_binary, "fixture-bin") == 0);
    assert(strcmp(node.media_name, "New stream title") == 0);
    assert(node.process_id == 123);

    const struct spa_dict_item replacement_items[] = {
        {PW_KEY_APP_NAME, "Renamed"},
        {PW_KEY_APP_ID, NULL},
        {PW_KEY_APP_PROCESS_ID, "invalid"}
    };
    const struct spa_dict replacement = SPA_DICT_INIT_ARRAY(replacement_items);
    vm_update_node_properties(&node, &replacement);
    assert(strcmp(node.application_name, "Renamed") == 0);
    assert(node.application_id == NULL);
    assert(node.process_id == -1);

    free(node.application_name);
    free(node.application_icon_name);
    free(node.process_binary);
    free(node.media_name);
    return 0;
}
