import { createStore } from "vuex";
import axios from "axios";
const base = process.env.VUE_APP_API_URL ?? "/api";
export const key = Symbol();
export const store = createStore({
    state: {
        readEvents: 0,
        processedEvents: 0,
        readPosition: 0,
        writePosition: 0,
        inFlightSink: 0,
        currentReadRate: 0,
        currentWriteRate: 0,
        lastSourcePosition: 0,
        sinkPosition: 0,
        gap: 0,
        progress: 0,
        recordedReadRate: { sum: 0, count: 0 },
        recordedWriteRate: { sum: 0, count: 0 },
        prepareChannel: { capacity: 0, size: 0 },
        sinkChannel: { capacity: 0, size: 0 },
        sinkLatency: 0,
        prepareLatency: 0
    },
    mutations: {
        updateState(state, payload) {
            function calculateRate(previous, current, last) {
                if (current.sum === previous.sum) {
                    return last;
                }
                const sum = current.sum - previous.sum;
                const count = current.count - previous.count;
                return Math.round(count / sum);
            }
            function totalRate(rate) {
                return rate.sum === 0 ? 0 : Math.round(rate.count / rate.sum);
            }
            function latency(rate) {
                return Math.round(rate.sum / rate.count * 1000);
            }
            state.currentReadRate = totalRate(payload.readRate);
            state.currentWriteRate = calculateRate(state.recordedWriteRate, payload.writeRate, state.currentWriteRate);
            state.inFlightSink = payload.inFlightSink;
            state.readEvents = payload.readEvents;
            state.processedEvents = payload.processedEvents;
            state.readPosition = payload.readPosition;
            state.writePosition = payload.writePosition;
            state.lastSourcePosition = payload.lastSourcePosition;
            state.sinkPosition = payload.sinkPosition;
            state.gap = payload.lastSourcePosition - payload.writePosition;
            state.recordedReadRate = payload.readRate;
            state.recordedWriteRate = payload.writeRate;
            state.prepareChannel = payload.prepareChannel;
            state.sinkChannel = payload.sinkChannel;
            state.sinkLatency = latency(payload.writeRate);
            state.prepareLatency = latency(payload.prepareRate);
            const progress = (1 - state.gap / state.lastSourcePosition) * 100;
            state.progress = Number(progress.toFixed(2));
        }
    },
    actions: {
        async getCounters(context) {
            const counters = await axios.get(`${base}/counters`);
            context.commit("updateState", counters.data);
        }
    },
    modules: {}
});
//# sourceMappingURL=index.js.map