import { Suspense } from 'react';
import HomeContent from './HomeContent';

export default function Home() {
    return (
        <Suspense fallback={<div className="flex justify-center items-center h-screen">Loading...</div>}>
            <HomeContent />
        </Suspense>
    );
}