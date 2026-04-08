var chartData = null;
var container = null;
var dotNetHelper;
var network = null;

var networkOptions = {
    nodes: { font: { strokeWidth: 0 } },
    edges: { font: { strokeWidth: 0 } },
    interaction: {
        dragNodes: false,
        dragView: true,
        hideEdgesOnDrag: false,
        hideEdgesOnZoom: false,
        hover: true,
        navigationButtons: true,
        tooltipDelay: 0,
    },
    groups: {
        useDefaultGroups: false,
    },
    layout: {
        randomSeed: 0,
    },
    physics: {
        enabled: true,
        barnesHut: {
            theta: 0.5,
            gravitationalConstant: -2000,
            centralGravity: 0.5,
            springLength: 95,
            springConstant: 0.04,
            damping: 0.09,
            avoidOverlap: 100
        },
        repulsion: {
            centralGravity: 0.2,
            springLength: 200,
            springConstant: 0.05,
            nodeDistance: 100,
            damping: 0.09
        },
        maxVelocity: 50,
        minVelocity: 0.1,
        solver: 'repulsion',
        stabilization: {
            enabled: true,
            iterations: 1000,
            updateInterval: 100,
            onlyDynamicEdges: false,
            fit: true
        },
        timestep: 0.5,
        adaptiveTimestep: true,
        wind: { x: 0, y: 0 }
    }
};

export function ChartRender(elementId, data, solver, seed, startNodeId) {
    chartData = FixChartIcons(data);

    networkOptions.physics.solver = solver;
    networkOptions.layout.randomSeed = seed;

    var nodeDistance = chartData.nodes.length * 15;
    if (nodeDistance < 50) nodeDistance = 50;
    else if (nodeDistance > 500) nodeDistance = 500;
    networkOptions.physics.repulsion.nodeDistance = nodeDistance;

    container = document.getElementById(elementId);
    if (!container) {
        console.error("NetworkChart: element '" + elementId + "' not found in DOM");
        return;
    }

    network = new vis.Network(container, chartData, networkOptions);

    SetFocusToStartNode(startNodeId);

    network.on("select", function (e) {
        var node = "";
        if (e.nodes && e.nodes.length > 0) {
            node = e.nodes[0];
        }

        if (node != "") {
            dotNetHelper.invokeMethod("ElementSelected", node.toString());
            return;
        }

        var edge = "";
        if (e.edges && e.edges.length > 0) {
            edge = e.edges[0];
        }
        if (edge != "") {
            dotNetHelper.invokeMethod("RelationshipSelected", edge.toString());
        }
    });
}

export function ChartUpdate(data, startNodeId) {
    if (!network) return;
    chartData = FixChartIcons(data);
    network.setData(chartData);
    SetFocusToStartNode(startNodeId);
}

export function RandomizeLayout(startNodeId) {
    if (!network || !container || !chartData) return "0";
    networkOptions.layout.randomSeed = undefined;
    network = new vis.Network(container, chartData, networkOptions);
    SetFocusToStartNode(startNodeId);
    var seed = network.getSeed();
    return seed;
}

export function UpdateSolver(solver) {
    if (!network) return;
    networkOptions.physics.solver = solver;
    network.setOptions(networkOptions);
    network.redraw();
}

export function SetDotNetHelper(value) {
    dotNetHelper = value;
}

function SetFocusToStartNode(startNodeId) {
    if (startNodeId) {
        network.unselectAll();
        network.setSelection({ nodes: [startNodeId] });
    }
}

function FixChartIcons(data) {
    data.nodes.forEach(function (item) {
        if (item.icon && item.icon.code) {
            item.icon.code = String.fromCharCode("0x" + item.icon.code);
        }
        if (item.title) {
            item.title = htmlTitle(item.title);
        }
    });
    return data;
}

function htmlTitle(html) {
    var container = document.createElement("div");
    container.innerHTML = html;
    return container;
}
