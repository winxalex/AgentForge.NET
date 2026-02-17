// chart-utils.js

// Initialize Mermaid.js configuration
if (typeof mermaid !== 'undefined') {
    mermaid.initialize({ startOnLoad: false, securityLevel: 'loose' });
}


// Ensure Gantt is available globally (loaded via script tag) - for frappe-gantt
if (typeof window.Gantt === 'undefined' && typeof Gantt !== 'undefined') {
    window.Gantt = Gantt;
}

window.ChartUtils = {
    // Unique ID counter for chart containers
    chartCounter: 0,

    // Render a Gantt chart
    renderGanttChart: function(containerId, data, options = {}) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error(`Container ${containerId} not found`);
            return;
        }

        // Clear previous content
        container.innerHTML = '';

        // Transform data to frappe-gantt format
        const tasks = this.transformGanttData(data);

        if (tasks.length === 0) {
            container.innerHTML = '<div class="alert alert-info">Нема податоци за приказ на Gantt графиконот.</div>';
            return;
        }

        try {
            // Create Gantt chart with responsive settings
            const gantt = new Gantt(`#${containerId}`, tasks, {
                header_height: 50,
                column_width: 20, // Smaller columns for better fit
                step: 24,
                view_modes: ['Day', 'Week', 'Month'], // Fewer view modes for simplicity
                bar_height: 16, // Smaller bars
                bar_corner_radius: 2,
                arrow_curve: 3,
                padding: 12, // Less padding
                view_mode: 'Week', // Start with Week view for better overview
                date_format: 'YYYY-MM-DD',
                popup_trigger: 'click',
                custom_popup_html: null,
                language: 'en',
                ...options
            });

            // Store reference for cleanup
            container._ganttInstance = gantt;

        } catch (error) {
            console.error('Error rendering Gantt chart:', error);
            container.innerHTML = '<div class="alert alert-danger">Грешка при исцртување на графиконот.</div>';
        }
    },

    // Transform data from our format to frappe-gantt format
    transformGanttData: function(data) {
        if (!Array.isArray(data)) return [];

        return data.map((item, index) => {
            // Expected format for frappe-gantt:
            // {
            //   id: string,
            //   name: string,
            //   start: string (YYYY-MM-DD),
            //   end: string (YYYY-MM-DD),
            //   progress: number (0-100),
            //   dependencies: string (comma-separated IDs),
            //   custom_class: string (optional)
            // }

            const task = {
                id: item.id || item.task_id || `task_${index}`,
                name: item.name || item.task_name || item.title || `Task ${index + 1}`,
                start: this.formatDate(item.start || item.start_date || item.begin_date),
                end: this.formatDate(item.end || item.end_date || item.finish_date),
                progress: parseInt(item.progress || item.completion || 0),
                dependencies: item.dependencies || item.depends_on || '',
                custom_class: item.custom_class || ''
            };

            return task;
        }).filter(task => task.start && task.end); // Filter out invalid tasks
    },

    // Format date to YYYY-MM-DD
    formatDate: function(dateInput) {
        if (!dateInput) return null;

        try {
            let date;

            if (dateInput instanceof Date) {
                date = dateInput;
            } else if (typeof dateInput === 'string') {
                // Try different date formats
                date = new Date(dateInput);
                if (isNaN(date.getTime())) {
                    // Try DD.MM.YYYY format
                    const parts = dateInput.split('.');
                    if (parts.length === 3) {
                        date = new Date(parts[2], parts[1] - 1, parts[0]);
                    }
                }
            }

            if (date && !isNaN(date.getTime())) {
                return date.toISOString().split('T')[0];
            }
        } catch (error) {
            console.warn('Error parsing date:', dateInput, error);
        }

        return null;
    },

    // Render a simple bar chart using Chart.js
    renderBarChart: function(containerId, data, options = {}) {
        // This would require Chart.js to be loaded
        // For now, we'll create a simple HTML/CSS bar chart
        this.renderSimpleBarChart(containerId, data, options);
    },

    // Simple HTML/CSS bar chart
    renderSimpleBarChart: function(containerId, data, options = {}) {
        const container = document.getElementById(containerId);
        if (!container) return;

        container.innerHTML = '';

        if (!Array.isArray(data) || data.length === 0) {
            container.innerHTML = '<div class="alert alert-info">Нема податоци за приказ.</div>';
            return;
        }

        const title = options.title || 'Бар График';
        const xAxis = options.xAxis || 'X';
        const yAxis = options.yAxis || 'Y';

        let html = `<div class="chart-container">
            <h5 class="chart-title">${title}</h5>
            <div class="bar-chart">`;

        // Find max value for scaling
        const maxValue = Math.max(...data.map(item => {
            const value = item.value || item.y || item.count || 0;
            return typeof value === 'number' ? value : 0;
        }));

        data.forEach((item, index) => {
            const label = item.label || item.name || item.x || `Item ${index + 1}`;
            const value = item.value || item.y || item.count || 0;
            const percentage = maxValue > 0 ? (value / maxValue) * 100 : 0;

            html += `
                <div class="bar-item">
                    <div class="bar-label">${label}</div>
                    <div class="bar-container">
                        <div class="bar-fill" style="width: ${percentage}%">
                            <span class="bar-value">${value}</span>
                        </div>
                    </div>
                </div>`;
        });

        html += `
            </div>
            <div class="chart-axis-labels">
                <span class="x-axis-label">${xAxis}</span>
                <span class="y-axis-label">${yAxis}</span>
            </div>
        </div>`;

        container.innerHTML = html;
    },

    // Render Plain Text (ASCII) chart
    renderPlainTextChart: function (containerId, textContent) {
        const container = document.getElementById(containerId);
        if (!container) {
            console.error(`Container ${containerId} not found`);
            return;
        }

        // Clear previous content
        container.innerHTML = '';

        // Create a <pre> element to preserve formatting (whitespace, newlines)
        const preElement = document.createElement('pre');
        preElement.className = 'plaintext-chart'; // Add a class for styling
        preElement.textContent = textContent || 'No content to display.';

        // Append the <pre> element to the container
        container.appendChild(preElement);
    },

    // Render a Mermaid chart
    renderMermaidChart: async function (containerId, chartDefinition) {

        console.log("Rendering mermaid chart");
        
        const container = document.getElementById(containerId);
        if (!container) {
            console.error(`Container ${containerId} not found`);
            return;
        }

        // Clear previous content
        container.innerHTML = '';

        if (!chartDefinition || typeof chartDefinition !== 'string' || chartDefinition.trim() === '') {
            container.innerHTML = '<div class="alert alert-info">Нема дефиниција за исцртување на Mermaid графиконот.</div>';
            return;
        }

        if (typeof mermaid === 'undefined') {
            console.error('Mermaid.js is not loaded.');
            container.innerHTML = '<div class="alert alert-danger">Mermaid.js библиотеката не е вчитана.</div>';
            return;
        }

        try {
            const uniqueId = `mermaid_${this.chartCounter++}`;

            // Use the modern async mermaid API to render the chart
            const { svg } = await mermaid.render(uniqueId, chartDefinition);
            container.innerHTML = svg;
        } catch (error) {
            console.error('Error rendering Mermaid chart:', error);
            container.innerHTML = `<div class="alert alert-danger">Грешка при исцртување на Mermaid графиконот. <pre>${error.message}</pre></div>`;
        }
    },

    // Cleanup chart instances
    cleanup: function(containerId) {
        const container = document.getElementById(containerId);
        if (container && container._ganttInstance) {
            // Clean up frappe-gantt instance if needed
            container._ganttInstance = null;
        }
    }
};
