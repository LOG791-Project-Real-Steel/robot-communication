#!/usr/bin/env python3
import asyncio, json, random, ssl, sys
import websockets
import gi
from jetracer.nvidia_racecar import NvidiaRacecar

gi.require_version('Gst', '1.0')
gi.require_version('GstWebRTC', '1.0')
gi.require_version('GstSdp', '1.0')
from gi.repository import Gst, GstWebRTC, GstSdp

Gst.init(None)

SIGNALING_SERVER = "wss://home.adammihajlovic.ca/robot/signaling"

class WebRTCClient:
    def __init__(self, peer_id, server):
        # self.car = NvidiaRacecar()
        # self.car.steering_gain = -1
        # self.car.steering_offset = 0
        # self.car.steering = 0
        # self.car.throttle_gain = 0.8
        print("Car ready to go!")

        self.peer_id = peer_id
        self.server = server
        self.conn = None
        self.pipe = None
        self.webrtc = None

    async def connect(self):
        sslctx = ssl.create_default_context(purpose=ssl.Purpose.CLIENT_AUTH)
        self.conn = await websockets.connect(self.server, ssl=sslctx)
        msg = json.dumps({'type': 'hello', 'idem': random.randint(1000,9999)})
        await self.conn.send(msg)

    async def setup_call(self):
        msg = json.dumps({'type': 'session', 'peer_id': self.peer_id})
        await self.conn.send(msg)

    def send_sdp_offer(self, offer):
        text = offer.sdp.as_text()
        msg = json.dumps({'type': 'offer', 'sdp': text})
        asyncio.ensure_future(self.conn.send(msg))

    def on_offer_created(self, promise, _, __):
        promise.wait()
        offer = promise.get_reply().get_value('offer')
        self.webrtc.emit('set-local-description', offer, Gst.Promise.new())
        self.send_sdp_offer(offer)

    def on_negotiation_needed(self, element):
        promise = Gst.Promise.new_with_change_func(self.on_offer_created, element, None)
        element.emit('create-offer', None, promise)

    def send_ice_candidate(self, _, mlineindex, candidate):
        msg = json.dumps({ 
            'type': 'candidate', 
            'data': {
                'candidate': candidate, 
                'sdpMLineIndex': mlineindex, 
                'sdpMid': str(mlineindex), 
                'usernameFragment': peer_id
            }
        })
        asyncio.ensure_future(self.conn.send(msg))

    def start_pipeline(self):
        def link_pads():
            print("🔗 Linking RTP pad to webrtcbin...")
            rtp_src_pad = self.pay_caps.get_static_pad("src")
            webrtc_sink_pad = self.webrtc.get_request_pad("send_rtp_sink_0")

            if not webrtc_sink_pad:
                raise Exception("❌ Failed to get webrtcbin send_rtp_sink_0 pad")

            if rtp_src_pad.link(webrtc_sink_pad) != Gst.PadLinkReturn.OK:
                raise Exception("❌ Failed to link RTP src pad to webrtcbin")

            print("✅ RTP pad linked successfully")

        # Create pipeline and elements
        self.pipe = Gst.Pipeline.new("webrtc-pipeline")

        source = Gst.ElementFactory.make("nvarguscamerasrc", "source")
        source.set_property("sensor-id", 0)

        caps_filter = Gst.ElementFactory.make("capsfilter", "caps")
        caps = Gst.Caps.from_string("video/x-raw(memory:NVMM),width=1280,height=720,framerate=30/1")
        caps_filter.set_property("caps", caps)

        conv = Gst.ElementFactory.make("nvvidconv", "converter")
        conv_caps = Gst.ElementFactory.make("capsfilter", "conv_caps")
        conv_caps.set_property("caps", Gst.Caps.from_string("video/x-raw,format=I420"))

        enc = Gst.ElementFactory.make("nvv4l2h264enc", "encoder")
        enc.set_property("insert-sps-pps", True)
        enc.set_property("bitrate", 4000000)
        enc.set_property("preset-level", 1)
        enc.set_property("idrinterval", 1)
        enc.set_property("maxperf-enable", True)

        parse = Gst.ElementFactory.make("h264parse", "parser")
        pay = Gst.ElementFactory.make("rtph264pay", "payloader")
        pay.set_property("config-interval", 1)

        pay_caps = Gst.ElementFactory.make("capsfilter", "pay_caps")
        pay_caps.set_property("caps", Gst.Caps.from_string(
            "application/x-rtp,media=video,encoding-name=H264,payload=96"
        ))

        webrtc = Gst.ElementFactory.make("webrtcbin", "sendrecv")
        webrtc.set_property("stun-server", "stun://stun.l.google.com:19302")

        for element in [source, caps_filter, conv, conv_caps, enc, parse, pay, pay_caps, webrtc]:
            self.pipe.add(element)

        source.link(caps_filter)
        caps_filter.link(conv)
        conv.link(conv_caps)
        conv_caps.link(enc)
        enc.link(parse)
        parse.link(pay)
        pay.link(pay_caps)

        self.webrtc = webrtc
        self.pay_caps = pay_caps

        # Start pipeline
        self.pipe.set_state(Gst.State.PLAYING)

        # Call later with bound state
        asyncio.get_event_loop().call_later(5, link_pads)

        self.webrtc.connect('on-negotiation-needed', self.on_negotiation_needed)
        self.webrtc.connect('on-ice-candidate', self.send_ice_candidate)

    async def loop(self):
        async for message in self.conn:
            msg = json.loads(message)
            print("Received message:", msg)
            msg_type = msg.get('type', 'null')
            if msg_type == 'hello':
                await self.setup_call()
            elif msg_type == 'session':
                session_stat = msg.get('status', '')
                if (session_stat == 'OK'):
                    self.start_pipeline()
            elif msg_type == 'move':
                json_car = message.get('Car', {})
                # self.car.steering = json_car.get('Steering', 0.0)
                # self.car.throttle = json_car.get('Throttle', 0.0)
            elif msg_type == 'answer':
                sdp = msg['sdp']['sdp']
                res, sdpmsg = GstSdp.SDPMessage.new()
                GstSdp.sdp_message_parse_buffer(bytes(sdp.encode()), sdpmsg)
                answer = GstWebRTC.WebRTCSessionDescription.new(GstWebRTC.WebRTCSDPType.ANSWER, sdpmsg)
                self.webrtc.emit('set-remote-description', answer, Gst.Promise.new())
            elif msg_type == 'candidate':
                data = msg.get('data', {})
                sdp_mline_index = data.get('sdpMLineIndex', -1)
                candidate = data.get('candidate', '')
                self.webrtc.emit('add-ice-candidate', sdp_mline_index, candidate)
            elif msg_type == 'error':
                print(message)
                break
            else:
                print(f"Unhandled message: {message}")

async def main(peer_id, server):
    client = WebRTCClient(peer_id, server)
    await client.connect()
    await client.loop()

if __name__ == '__main__':
    peer_id = sys.argv[1] if len(sys.argv) > 1 else 'test'
    asyncio.get_event_loop().run_until_complete(main(peer_id, SIGNALING_SERVER))
