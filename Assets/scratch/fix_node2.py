import json

file_path = 'c:/Repositorios/GL5/Assets/StreamingAssets/narrativeNodes.json'
with open(file_path, 'r') as f:
    data = json.load(f)

nodes = data['narrativeNodes']

def get_node(node_id):
    return next((n for n in nodes if n['_id'] == node_id), None)

node2 = get_node(2)

# Create a new options node (ID 99) to hold the choices for Node 2
options_node = {
    "_id": 99,
    "_nextNode": -1,
    "_options": node2['_options'],
    "_dialogues": [],
    "_isEndNode": False,
    "_loopNode": -1,
    "_dialogueType": 0,
    "_hasOptions": True,
    "_eventOnEnd": ""
}

# Add it to the list if not exists
if not get_node(99):
    nodes.append(options_node)
else:
    get_node(99)['_options'] = node2['_options']

# Now strip options from Node 2 and point it to Node 99
node2['_options'] = []
node2['_hasOptions'] = False
node2['_nextNode'] = 99
node2['_isEndNode'] = False

with open(file_path, 'w') as f:
    json.dump(data, f, indent=4)
