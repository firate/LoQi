"use client";

import Image from 'next/image';
import logo from '../logo/logo-dark.svg';

export default function Logo() {
    return (
        <Image
            src={logo}
            alt="Logo"
            width={150}
            height={150}
        />
    );
}